using DataService;
using FunctionalService;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cms
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            //db connection
            services.AddDbContext<ApplicationDbContext>(opt =>
            {
                opt.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"), x => x.MigrationsAssembly("cms"));
            });

            services.AddDbContext<DataProtectionKeysContext>(opt =>
            {
                opt.UseSqlServer(Configuration.GetConnectionString("DataProtectionKey"), x => x.MigrationsAssembly("cms"));
            });

            //functional service

            services.AddTransient<IFunctionalSvc, FunctionalSvc>();
            services.Configure<AdminUserOptions>(Configuration.GetSection("AdminUserOptions"));
            services.Configure<AppUserOptions>(Configuration.GetSection("AppUserOptions"));

            //identity options
            var identityDefaultconfig = Configuration.GetSection("IdentityDefaultOptions");
            services.Configure<IdentityDefaultOptions>(identityDefaultconfig);

            var identityOptions = identityDefaultconfig.Get<IdentityDefaultOptions>();

            services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
            {
                //password
                opt.Password.RequireDigit = identityOptions.PasswordRequireDigit;
                opt.Password.RequiredLength = identityOptions.PasswordRequiredLength;
                opt.Password.RequireNonAlphanumeric = identityOptions.PasswordRequireNonAlphanumeric;
                opt.Password.RequireUppercase = identityOptions.PasswordRequireUppercase;
                opt.Password.RequireLowercase = identityOptions.PasswordRequireLoweCase;
                opt.Password.RequiredUniqueChars = identityOptions.PasswordRequireUniqueChars;

                //lockout settings
                opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(identityOptions.LockoutDefaultLockoutTimeSpanInMinutes);
                opt.Lockout.MaxFailedAccessAttempts = identityOptions.LockoutMaxFailedAccessAttempts;
                opt.Lockout.AllowedForNewUsers = identityOptions.LockoutAllowedForNewUsers;

                //user settings
                opt.User.RequireUniqueEmail = identityOptions.UserRequireUniqueEmail;

                //EMAIL CONFIRM REQUIRE
                opt.SignIn.RequireConfirmedEmail = identityOptions.SignInRequireConfirmedEmail;
            }).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "areas",
                    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
