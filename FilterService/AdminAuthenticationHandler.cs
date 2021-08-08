using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using ModelService;
using Serilog;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using DataService;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

namespace FilterService
{
    public class AdminAuthenticationHandler : AuthenticationHandler<AdminAuthenticationOptions>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IServiceProvider _provider;
        private readonly IdentityDefaultOptions _identityDefaultOptions;
        private readonly DataProtectionKeys _dataProtectionKeys;
        private readonly AppSettings _appSettings;
        private const string AccessToken = "access_token";
        private const string User_Id = "use_id";
        private const string UserName = "username";
        private string[] UserRoles = new[] { "Administrator"};
        public AdminAuthenticationHandler(IOptionsMonitor<AdminAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock,
            UserManager<ApplicationUser> userManager, IOptions<AppSettings> appSettings, IOptions<DataProtectionKeys> dataProtectionKeys, IServiceProvider provider,
            IOptions<IdentityDefaultOptions> identityDefaultOptions
            ) :base(options, logger, encoder, clock)
        {
            _userManager = userManager;
            _provider = provider;
            _identityDefaultOptions = identityDefaultOptions.Value;
            _dataProtectionKeys = dataProtectionKeys.Value;
            _appSettings = appSettings.Value;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // logic validating the request
            if(!Request.Cookies.ContainsKey(AccessToken) || !Request.Cookies.ContainsKey(User_Id))
            {
                Log.Error("No access token or userid");
                return await Task.FromResult(AuthenticateResult.NoResult());
            }

            if(!AuthenticationHeaderValue.TryParse($"{Request.Cookies[AccessToken]}", out AuthenticationHeaderValue headerValue))
            {
                Log.Error("could not parse access token from header");
                return await Task.FromResult(AuthenticateResult.NoResult());
            }

            if(!AuthenticationHeaderValue.TryParse($"{Request.Cookies[User_Id]}", out AuthenticationHeaderValue headerValueUid))
            {
                Log.Error("Could not parse user id from header");
                return await Task.FromResult(AuthenticateResult.NoResult());
            }

            try
            {
                //create validation parameter
                //step 1 get key to decrypt the jwt parameter IOptions helps to map appsettings class to the config 
                //get the validation parameters 
                var key = Encoding.ASCII.GetBytes(_appSettings.Secret);

                //step 2 create an instance of jwt handler
                var handler = new JwtSecurityTokenHandler();
                //step 3 create an instance of jwt validation parameters
                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = _appSettings.Site,
                    ValidAudience = _appSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                //step 4 decrypt the token get the data protection service instance
                var protectorProvider = _provider.GetService<IDataProtectionProvider>();
                //step 5 add protector instance using application user key from config
                var protector = protectorProvider.CreateProtector(_dataProtectionKeys.ApplicationUserKey);
                //step 6 decrypt the user id
                var decryptedUid = protector.Unprotect(headerValueUid.Parameter);
                //step 7 decrypt the access token
                var decryptedToken = protector.Unprotect(headerValue.Parameter);

                //create an instance of the user token

                TokenModel tokenModel = new TokenModel();

                //check if the user token details is present in the database
                //get the existing token from the user on the database

                using (var scope = _provider.CreateScope())
                {
                    var dbContextService = scope.ServiceProvider.GetService<ApplicationDbContext>();
                    var userToken = dbContextService.Tokens.Include(x => x.User)
                        .FirstOrDefault(ut => ut.UserId == decryptedUid
                                                && ut.User.UserName == Request.Cookies[UserName]
                                                && ut.User.Id == decryptedUid
                                                && ut.User.UserRole == "Administrator");
                    tokenModel = userToken;
                }

                //check if tokenmodel is null
                if(tokenModel == null)
                {
                    return await Task.FromResult(AuthenticateResult.Fail("You are not authorized to view this page"));
                }
                // apply second layer of decryption using the key store in the token model
                // create protector instance for layer two using token model ket
                //if no ket exist throw exception
                IDataProtector layerTwoProtector = protectorProvider.CreateProtector(tokenModel?.EncryptionKeyJwt);
                string decryptedTokenLayerTwo = layerTwoProtector.Unprotect(decryptedToken);

                //validate the access token recieved
                //if validation fails it throws an exception
                var validateToken = handler.ValidateToken(decryptedToken, validationParameters, out var securityToken);

                //checking token signature
                if(!(securityToken is JwtSecurityToken jwtSecurityToken) || 
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return await Task.FromResult(AuthenticateResult.Fail("You are not authorized"));
                }

                //extract the username from validated token
                var username = validateToken.Claims.FirstOrDefault(cliam => cliam.Type == ClaimTypes.NameIdentifier)?.Value;

                if(Request.Cookies[UserName] != username)
                {
                    return await Task.FromResult(AuthenticateResult.Fail("You are not authorized to view this page"));
                }

                //get user by their mail
                var user = await _userManager.FindByNameAsync(username);

                //if user does not exist return authentication failed result
                if(user == null)
                {
                    return await Task.FromResult(AuthenticateResult.Fail("You are not authorized to view this page"));
                }

                // we need to check if the user belongs to the group of user roles
                if (!UserRoles.Contains(user.UserRole))
                {
                    return await Task.FromResult(AuthenticateResult.Fail("You are not authorized to view this page"));
                }

                //create authentiation or success ticket 
                    ///create a new claims identity add the claims present in the token
                var identity = new ClaimsIdentity(validateToken.Claims, Scheme.Name);
                    ///create a principal and pass identity object to the claim
                var principal = new ClaimsPrincipal(identity);
                ///use the object or principal to create authentication ticket
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                ///return success to the client
                return await Task.FromResult(AuthenticateResult.Success(ticket));
                
            }
            catch (Exception ex)
            {

                Log.Error("An Error occured while seeding the database {Error} {Stacktree} {InnerException} {Source}", ex.Message, ex.StackTrace, ex.Source);
                return await Task.FromResult(AuthenticateResult.Fail("You are not authorized"));
            }
            
        }

        //this handles situation when authentication fails
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            //logic handling failed request

            Response.Cookies.Delete("access_token");
            Response.Cookies.Delete("user_id");
            Response.Headers["WWW-Authenticate"] = $"Not Authorized";
            Response.Redirect(_identityDefaultOptions.AccessDeniedPath);

            return Task.CompletedTask;
        }
    }
}
