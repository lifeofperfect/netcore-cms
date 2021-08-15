using AuthService;
using cms.Models;
using CookieService;
using DataService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ModelService;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cms.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly AppSettings _appSettings;
        private DataProtectionKeys _dataProtectionKeys;
        private readonly IServiceProvider _provider;
        private readonly ApplicationDbContext _db;
        private readonly IAuthSvc _authSvc;
        private readonly ICookieService _cookieService;
        private const string AccessToken = "access_token";
        private const string User_Id = "user_id";
        string[] cookiesToDelete = { "twoFactorToken", "memberId", "remeberDevice", "user_id", "access_token" };
        public AccountController(IOptions<AppSettings> appSettings, IOptions<DataProtectionKeys> dataProtectionKeys,
            IServiceProvider provider, ApplicationDbContext db, IAuthSvc authSvc, ICookieService cookieService)
        {
            _appSettings = appSettings.Value;
            _dataProtectionKeys = dataProtectionKeys.Value;
            _db = db;
            _authSvc = authSvc;
            _cookieService = cookieService;
        }

        [HttpPost]
        [Route("login")]
        [AllowAnonymous]
        [ProducesResponseType(200, Type=typeof(GenericResponse))]
        public async Task<IActionResult> Login(LoginModel request)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var jwtToken = await _authSvc.Auth(request);
                    const int expireTime = 60;

                    _cookieService.SetCookie("access_token", jwtToken.Token, expireTime);
                    _cookieService.SetCookie("user_id", jwtToken.UserId, expireTime);
                    _cookieService.SetCookie("user_name", jwtToken.Username, expireTime);
                    Log.Information($"User {request.Email} logges in");
                    var response = new GenericResponse
                    {
                        ResponseCode="00",
                        ResponseDescription="Success"
                    };

                    return Ok(response);
                }
                catch (Exception ex)
                {

                    Log.Error("An Error occured while seeding the database {Error} {Stacktree} {InnerException} {Source}", ex.Message, ex.StackTrace, ex.Source); ;
                }
            }
            ModelState.AddModelError("", "Invalid Username/password");
            Log.Error("Invalid username or password");
            var fail  = new GenericResponse
            {
                ResponseCode = "999",
                ResponseDescription = "Unauthorized"
            };
            return Ok(fail);

        }
    }
}
