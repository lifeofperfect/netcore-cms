using ActivityService;
using CookieService;
using DataService;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModelService;
using Serilog;
using System;
using System.Collections.Generic;
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
        private readonly DataProtectionKey _dataProtectionKeys;
        private readonly ICookieService _cookieService;
        private readonly IActivityService _activityService;
        private IDataProtector _protector;
        private string[] UserRoles = new[] { "Administrator", "Customer" };
        private TokenValidationParameters validationParameters;
        private JwtSecurityTokenHandler handler;
        private string unProtectedToken;
        private ClaimsPrincipal validateToken;

        public AuthSvc(UserManager<ApplicationUser> userManager, IOptions<AppSettings> appSettings, IOptions<DataProtectionKey> dataProtectionKeys,
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
    }
}
