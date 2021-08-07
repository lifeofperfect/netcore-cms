using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ModelService;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FunctionalService
{
    
    public class FunctionalSvc : IFunctionalSvc
    {
        private readonly AdminUserOptions _adminUserOptions;
        private readonly AppUserOptions _appUserOptions;
        private readonly UserManager<ApplicationUser> _userManager;
        public FunctionalSvc(IOptions<AdminUserOptions> adminUserOptions, IOptions<AppUserOptions> appUserOptions, UserManager<ApplicationUser> userManager)
        {
            _adminUserOptions = adminUserOptions.Value;
            _appUserOptions = appUserOptions.Value;
            _userManager = userManager;
        }

        public async Task CreateDefaultAdminUser()
        {
            try
            {
                var adminUser = new ApplicationUser
                {
                    Email = _adminUserOptions.Email,
                    UserName = _adminUserOptions.UserName,
                    EmailConfirmed = true,
                    ProfilePic =  GetDefaultProfilePic(),
                    PhoneNumber = "1234567890",
                    PhoneNumberConfirmed = true,
                    FirstName = _adminUserOptions.FirstName,
                    LastName = _adminUserOptions.LastName,
                    UserRole = "Administrator",
                    IsActive = true,
                    UserAddresses = new List<AddressModel>
                    {
                        new AddressModel {Country = _adminUserOptions.Country, Type = "Billing"},
                        new AddressModel {Country = _adminUserOptions.Country, Type = "Shipping"}
                    }
                };

                var result = await _userManager.CreateAsync(adminUser, _adminUserOptions.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(adminUser, "Administrator");
                    Log.Information("Admin User created {username}", adminUser.UserName);
                }else
                {
                    var errorString = string.Join(",", result.Errors);
                    Log.Error("Error while creating user {error}", errorString);
                }
            }
            catch (Exception ex)
            {

                Log.Error("An Error occured while seeding the database {Error} {Stacktree} {InnerException} {Source}", ex.Message, ex.StackTrace, ex.Source); 
            }
        }

        
        public async Task CreateDefaultUser()
        {
            try
            {
                var appUser = new ApplicationUser
                {
                    Email = _appUserOptions.Email,
                    UserName = _appUserOptions.UserName,
                    EmailConfirmed = true,
                    ProfilePic = GetDefaultProfilePic(),
                    PhoneNumber = "1234567890",
                    PhoneNumberConfirmed = true,
                    FirstName = _appUserOptions.FirstName,
                    LastName = _appUserOptions.LastName,
                    UserRole = "Customer",
                    IsActive = true,
                    UserAddresses = new List<AddressModel>
                    {
                        new AddressModel {Country = _appUserOptions.Country, Type = "Billing"},
                        new AddressModel {Country = _appUserOptions.Country, Type = "Shipping"}
                    }
                };

                var result = await _userManager.CreateAsync(appUser, _appUserOptions.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(appUser, "Customer");
                    Log.Information("App usr created {username}", appUser.UserName);
                }
                else
                {
                    var errorString = string.Join(",", result.Errors);
                    Log.Error("Error while creating user {error}", errorString);
                }
            }
            catch (Exception ex)
            {

                Log.Error("An Error occured while seeding the database {Error} {Stacktree} {InnerException} {Source}", ex.Message, ex.StackTrace, ex.Source); ;
            }
        }

        private string GetDefaultProfilePic()
        {
            return string.Empty;
        }

    }
}
