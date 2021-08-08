using ActivityService;
using CookieService;
using DataService;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using ModelService;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace AuthService
{
    public class AuthSvc : IAuthSvc
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppSettings _appSettings;
        private readonly ApplicationDbContext _db;
        private readonly IServiceProvider _provider;
        private readonly DataProtectionKeys _dataProtectionKeys;
        private readonly ICookieService _cookieService;
        private readonly IActivityService _activityService;
        private IDataProtector _protector;
        private string[] UserRoles = new[] { "Administrator", "Customer" };
        private TokenValidationParameters validationParameters;
        private JwtSecurityTokenHandler handler;
        private string unProtectedToken;
        private ClaimsPrincipal validateToken;

        public AuthSvc(UserManager<ApplicationUser> userManager, IOptions<AppSettings> appSettings, IOptions<DataProtectionKeys> dataProtectionKeys,
            ApplicationDbContext db, IServiceProvider provider, IActivityService activityService, ICookieService cookieService)
        {
            _userManager = userManager;
            _appSettings = appSettings.Value;
            _dataProtectionKeys = dataProtectionKeys.Value;
            _db = db;
            _cookieService = cookieService;
            _provider = provider;
            _activityService = activityService;

        }

        public async Task<TokenResponseModel> Auth(LoginModel request)
        {
            try
            {
                //get the user frm the database
                var user = await _userManager.FindByEmailAsync(request.Email);

                if (user == null) return CreateErrorResponseToken("Request Not Supported", HttpStatusCode.Unauthorized);

                //get the roles of the user - validate if admin
                var roles = await _userManager.GetRolesAsync(user);

                if(roles.FirstOrDefault() != "Administrator")
                {
                    Log.Error("Error: Role not admin");
                    return CreateErrorResponseToken("Request not supported", HttpStatusCode.Unauthorized);

                }

                //if user is admin authenticate password
                if(!await _userManager.CheckPasswordAsync(user, request.Password))
                {
                    Log.Error("Error: Invalid password for admin");
                    return CreateErrorResponseToken("Request not supported", HttpStatusCode.Unauthorized);
                }

                //check if email is confirmed
                if(!await _userManager.IsEmailConfirmedAsync(user))
                {
                    Log.Error("Error: Emil not confirmed for {user}", user.UserName);
                    return CreateErrorResponseToken("Request not supported", HttpStatusCode.Unauthorized);
                }

                var authToken = await GenerateNewToken(user, request);
                return authToken;
            }
            catch (Exception ex)
            {

                Log.Error("An Error occured while seeding the database {Error} {Stacktree} {InnerException} {Source}", ex.Message, ex.StackTrace, ex.Source);
            }

            return CreateErrorResponseToken("Request not supported", HttpStatusCode.Unauthorized);
        }

        private async Task<TokenResponseModel> GenerateNewToken(ApplicationUser user, object model)
        {
            //create a key to encrypt the jwt
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_appSettings.Secret));

            //Get user role => check if user is admin
            var roles = await _userManager.GetRolesAsync(user);

            //createing jwt token
            var tokenHander = new JwtSecurityTokenHandler();

            //creating jwt token  and description for the token
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]{
                    new Claim(JwtRegisteredClaimNames.Sub, user.UserName), //sub -identifies principal that issued the jwt
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), //Jti -unique identifier for the token
                    new Claim(ClaimTypes.NameIdentifier, user.Id), //unique identifier for the user
                    new Claim(ClaimTypes.Role, roles.FirstOrDefault()), // role of the user
                    new Claim("LoggedOn", DateTime.Now.ToString(CultureInfo.InvariantCulture)) //time when created
                }),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
                Issuer = _appSettings.Site, //Issuer  -identifies principal that issues the jwt
                Audience = _appSettings.Audience, //audience identifies the recipient that the jwt is intended for
                Expires = (string.Equals(roles.FirstOrDefault(), "Admistrator", StringComparison.CurrentCultureIgnoreCase)) ? DateTime.UtcNow.AddMinutes(60): DateTime.UtcNow.AddMinutes(Convert.ToDouble(_appSettings.ExpireTime))
            };

            //create the token create unique encryption key for token  --2nd layer protection
            var encryptionKeyRt = Guid.NewGuid().ToString();
            var encryptionKeyJwt = Guid.NewGuid().ToString();

            //Get the data protection service instance
            var protectorProvider = _provider.GetService<IDataProtectionProvider>();

            //create a protector instance
            var protectorJwt = protectorProvider.CreateProtector(encryptionKeyJwt);

            //generate token and protect the user token
            var token = tokenHander.CreateToken(tokenDescriptor);
            //ENCRYPT TOKEN
            var encryptedToken = protectorJwt.Protect(tokenHander.WriteToken(token));

            //create and update token table STORE THE token encryption key
            TokenModel newRtoken = new TokenModel();

            //create refresh token instance
            newRtoken = CreateRefreshToken(_appSettings.ClientId, user.Id, Convert.ToInt32(_appSettings.RtExpireTime));

            //assign the jwt encryption key
            newRtoken.EncryptionKeyJwt = encryptionKeyJwt;

            //assign rt encyption key
            newRtoken.EncryptionKeyRt = encryptionKeyRt;

            //addd refresh token with encryption key for jwt to db
            try
            {
                //first we need to check if the user has already logged in  and as tokens in DB
                var rt = _db.Tokens.FirstOrDefault(t => t.UserId == user.Id);

                if(rt != null)
                {
                    //invalidate the old refresh token by deleting it
                    _db.Tokens.Remove(rt);

                    //add the new refresh tokens
                    _db.Tokens.Add(newRtoken);
                }
                else
                {
                    await _db.Tokens.AddAsync(newRtoken);
                }
                //persist changes in the DB
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {

                Log.Error("An Error occured while seeding the database {Error} {Stacktree} {InnerException} {Source}", ex.Message, ex.StackTrace, ex.Source);
            }

            // Return response containing encrypted token
            var protectorRt = protectorProvider.CreateProtector(encryptionKeyRt);
            var layerOneProtector = protectorProvider.CreateProtector(_dataProtectionKeys.ApplicationUserKey);

            var encAuthToken = new TokenResponseModel
            {
                Token = layerOneProtector.Protect(encryptedToken),
                Expiration = token.ValidTo,
                RefreshToken = protectorRt.Protect(newRtoken.Value),
                Role = roles.FirstOrDefault(),
                Username = user.UserName,
                UserId = layerOneProtector.Protect(user.Id),
                ResponseInfo = CreateResponse("Auth Token Created", HttpStatusCode.OK)
            };

            return encAuthToken;
        }

        //Private method
        private static TokenResponseModel CreateErrorResponseToken(string errorMessage, HttpStatusCode statusCode)
        {
            var errorToken = new TokenResponseModel
            {
                Token = null,
                Username = null,
                Role = null,
                RefreshTokenExpiration = DateTime.Now,
                RefreshToken = null,
                Expiration = DateTime.Now,
                ResponseInfo = CreateResponse(errorMessage, statusCode)
            };

            return errorToken;
        }

        private static ResponseStatusInfoModel CreateResponse(string errorMessage, HttpStatusCode statusCode)
        {
            var responseStatusInfo = new ResponseStatusInfoModel
            {
                Message = errorMessage,
                StatusCode = statusCode
            };
            return responseStatusInfo;
        }

        private static TokenModel CreateRefreshToken(string clientId, string userId, int expiryTime)
        {
            return new TokenModel()
            {
                ClientId = clientId,
                UserId = userId,
                Value = Guid.NewGuid().ToString("N"),
                CreatedDate = DateTime.UtcNow,
                ExpiryTime = DateTime.UtcNow.AddMinutes(expiryTime),
                EncryptionKeyRt = "",
                EncryptionKeyJwt = ""
            };
        }
    }
}
