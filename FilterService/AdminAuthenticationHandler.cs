using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelService;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

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
        private string[] UserRoles = new[] { "Administrator" };
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

            if(!AuthenticationHeaderValue.TryParse($"{Request.Cookies[User_Id]}", out AuthenticationHeaderValue headerVALUE))
            {
                Log.Error("Could not parse user id from header");
                return await Task.FromResult(AuthenticateResult.NoResult());
            }

            try
            {

            }
            catch (Exception ex)
            {

                Log.Error("An Error occured while seeding the database {Error} {Stacktree} {InnerException} {Source}", ex.Message, ex.StackTrace, ex.Source);
                return await Task.FromResult(AuthenticateResult.Fail("You are not authorized"));
            }
            return null;
        }

        //this handles situation when authentication fails
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            //logic handling failed request
            return Task.CompletedTask;
        }
    }
}
