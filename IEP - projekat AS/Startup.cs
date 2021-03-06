﻿using System;
using Hangfire;
using Hangfire.Common;
using IEP___projekat_AS.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(IEP___projekat_AS.Startup))]
namespace IEP___projekat_AS
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            createRolesandUsers();
            app.MapSignalR(); //za signalR nesto..

            //neke za scheduling stvari
            GlobalConfiguration.Configuration.UseSqlServerStorage("MyDbContext");
            app.UseHangfireServer();
            app.UseHangfireDashboard();

            //BackgroundJob.Enqueue(() => cron());
        }

        //*************DEPRECATED*************************
        //public void cron()
        //{
        //    updateAuctions();

        //    BackgroundJob.Schedule(() => cron(), System.TimeSpan.FromSeconds(10));
        //}

        //*************DEPRECATED*************************
        //private void updateAuctions()
        //{
        //    var db = new ApplicationDbContext();
        //    var auctions = db.Auctions;
        //    foreach (var a in auctions)
        //    {
        //        if (a.length == 0 && a.status != "EXPIRED")
        //        {
        //            a.status = "EXPIRED";
        //            //add winner id?
        //        }
        //        else if (a.status == "OPEN")
        //            a.length-=10;
        //    }
        //    db.SaveChanges();
        //}

        /// In this method we will create default User roles and Admin user for login   
        private void createRolesandUsers()
        {
            ApplicationDbContext context = new ApplicationDbContext();
            
            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(context));
            var UserManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context));



            //Simple user creation

            var user1 = new ApplicationUser();
            user1.UserName = "test@asp.net";
            user1.Email = "test@asp.net";
            user1.Name = "Test";
            user1.Surname = "Testic";

            string userPWD1 = "2607290";

            UserManager.Create(user1, userPWD1);




            // In Startup iam creating first Admin Role and creating a default Admin User    
            if (!roleManager.RoleExists("Admin"))
            {

                // first we create Admin rool   
                var role = new Microsoft.AspNet.Identity.EntityFramework.IdentityRole();
                role.Name = "Admin";
                roleManager.Create(role);

                //Here we create a Admin super user who will maintain the website                  

                var user = new ApplicationUser();
                user.UserName = "azaricstefan1@gmail.com";
                user.Email = "azaricstefan1@gmail.com";
                user.Name = "Stefan";
                user.Surname = "Azaric";

                string userPWD = "2607290";

                var chkUser = UserManager.Create(user, userPWD);

                //Add default User to Role Admin   
                if (chkUser.Succeeded)
                {
                    var result1 = UserManager.AddToRole(user.Id, "Admin");

                }

            }

            //// creating Creating Manager role    
            //if (!roleManager.RoleExists("Manager"))
            //{
            //    var role = new Microsoft.AspNet.Identity.EntityFramework.IdentityRole();
            //    role.Name = "Manager";
            //    roleManager.Create(role);

            //}

            //// creating Creating Employee role    
            //if (!roleManager.RoleExists("Employee"))
            //{
            //    var role = new Microsoft.AspNet.Identity.EntityFramework.IdentityRole();
            //    role.Name = "Employee";
            //    roleManager.Create(role);

            //}
        }
    }
}
