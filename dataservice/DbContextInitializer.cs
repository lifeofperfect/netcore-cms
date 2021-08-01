using FunctionalService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataService
{
    public static class DbContextInitializer
    {

        public static async Task Initialize(DataProtectionKeysContext dataProtectionKeysContext, ApplicationDbContext applicationDbContext, IFunctionalSvc functionalSvc)
        {
            //check if db dataprotection kesy context is created
            //check if db applicatondbcontext is created
            await dataProtectionKeysContext.Database.EnsureCreatedAsync();
            await applicationDbContext.Database.EnsureCreatedAsync();

            //check if db contains any users
            if (applicationDbContext.ApplicationUsers.Any())
            {
                return;
            }
            //if empy creata a new user
            await functionalSvc.CreateDefaultAdminUser();
            await functionalSvc.CreateDefaultUser();

        }
        
    }
}
