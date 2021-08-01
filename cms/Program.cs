using DataService;
using FunctionalService;
using LoggingService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace cms
{
    public class Program
    {
        public static void Main(string[] args)
        {
            
            var host = CreateHostBuilder(args).Build();

            //using(var scope = host.Services.CreateScope())
            //{
            //    try
            //    {
            //        int zero = 0;
            //        int result = 100 / zero;
            //    }
            //    catch (DivideByZeroException ex)
            //    {

            //        Log.Error("An Error occured while seeding the database {Error} {Stacktree} {InnerException} {Source}", ex.Message, ex.StackTrace, ex.Source);
            //    }
            //}

            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {

                    var context = services.GetRequiredService<ApplicationDbContext>();
                    var dpContext = services.GetRequiredService<DataProtectionKeysContext>();
                    var funcsvc = services.GetRequiredService<IFunctionalSvc>();

                    DbContextInitializer.Initialize(dpContext, context, funcsvc).Wait();

                }
                catch (Exception ex)
                {

                    Log.Error("An Error occured while seeding the database {Error} {Stacktree} {InnerException} {Source}", ex.Message, ex.StackTrace, ex.Source);
                }
            }


            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseSerilog((hostingContext, loggerConfiguration)=> loggerConfiguration
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty("Application", "CMS")
                        .Enrich.WithProperty("MachineName", Environment.MachineName)
                        .Enrich.WithProperty("CurrentManagedThreadId", Environment.CurrentManagedThreadId)
                        .Enrich.WithProperty("OSVersion", Environment.OSVersion)
                        .Enrich.WithProperty("Version", Environment.Version)
                        .Enrich.WithProperty("UserName", Environment.UserName)
                        .Enrich.WithProperty("ProcessId", Process.GetCurrentProcess().Id)
                        .Enrich.WithProperty("ProcessName", Process.GetCurrentProcess().ProcessName)
                        .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                        .WriteTo.File(formatter: new CustomTextFormatter(), path: Path.Combine(hostingContext.HostingEnvironment.ContentRootPath + $"{Path.DirectorySeparatorChar}Logs{Path.DirectorySeparatorChar}", $"load_error_{DateTime.Now:yyyyMMdd}.txt"))
                        .ReadFrom.Configuration(hostingContext.Configuration)
                    );
                    webBuilder.UseStartup<Startup>();
                });
    }
}
