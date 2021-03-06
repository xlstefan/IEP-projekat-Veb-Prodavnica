﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using IEP___projekat_AS.Models;
using Hangfire;
using System.Data.Entity.Infrastructure;

namespace IEP___projekat_AS.Controllers
{
    [Authorize]
    public class ManageController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;
        private ApplicationDbContext db = new ApplicationDbContext();

        public ActionResult OpenReadyAuctions()
        {
            return View(db.Auctions.ToList());
        }

        [Authorize(Roles = "Admin")]
        public ActionResult OpenAuction(int? id)
        {
            if (id == null)
            {
                return new HttpNotFoundResult("That auction doesn't exist!");
            }
            var auction = db.Auctions.Find(id);
            if (auction.status.Equals("READY"))
            {
                auction.status = "OPEN";
                auction.opening = DateTime.Now;
                auction.closing = DateTime.Now.Add(TimeSpan.FromSeconds(auction.length));
                //create cron task
                BackgroundJob.Schedule(() => closeAuctionTask(auction), new DateTimeOffset((DateTime)auction.closing));
                db.SaveChanges();
                ViewBag.StatusMessage = "Auction: " + auction.name + ", with ID:" + id + " opened!";
                return View("OpenReadyAuctions", db.Auctions.ToList());
            }
            ViewBag.ErrorMessage = "Auction: " + auction.name + ", with ID:" + id + " failed to open! | STATUS => " + auction.status;
            return View("OpenReadyAuctions", db.Auctions.ToList());
        }

        public void closeAuctionTask(Auction tmp)
        {
            var a = db.Auctions.Where(m => m.Id == tmp.Id).FirstOrDefault();
            if (a == null) return;
            if (DateTime.Now > a.closing && a.status != "EXPIRED")
            {
                var offers = a.Offers;
                var lastOffer = offers.LastOrDefault();
                if (lastOffer != null)
                {
                    TimeSpan tlo = (DateTime)a.closing - lastOffer.time;
                    if (tlo.Seconds < 10 && tlo.Seconds > 0)
                    {
                        //DONT CLOSE 
                        //a.Offers = null;
                        //BackgroundJob.Schedule(() => closeAuctionTask(a), new DateTimeOffset((DateTime)a.closing));
                        return;
                    }
                }

                //DODATI ONO SA 10 SEKUNDI!
                if (a.price > 1)
                    a.status = "SOLD";
                else a.status = "EXPIRED";
                //winnerID?
            }
            bool saveFailed;
            do
            {
                saveFailed = false;
                try { db.SaveChanges(); }
                catch (DbUpdateConcurrencyException ex)
                {
                    saveFailed = true;
                    // Update original values from the database 
                    var entry = ex.Entries.Single();
                    entry.OriginalValues.SetValues(entry.GetDatabaseValues());
                }
            } while (saveFailed);
        }

        public ActionResult AllTokenOrders()
        {
            //prikazi sve ordere token-a
            var userId = User.Identity.GetUserId();

            var orders = db.Orders.Where(o => o.user_Id == userId).ToList();

            return View(orders);
        }

        public ActionResult BuyTokens([Bind(Include = "PackageType, PhoneNumber, UserID")] BuyTokensViewModel btvm)/*(String packageType, String phoneNumber)/*(string api, string clientId)*/
        {
            var userId = User.Identity.GetUserId();
            var user = db.Users.Find(userId);
            btvm.UserID = userId;
            if (String.IsNullOrEmpty(btvm.PackageType) || String.IsNullOrEmpty(btvm.PhoneNumber))
            {
                return View(btvm);
            }
            var value = Decimal.Parse(btvm.PackageType);

            //user.Credit += value; //ovo se radi u linku potvrde 
            db.SaveChanges();
            var orders = db.Orders.Where(o => o.user_Id == userId).ToList();
            Order order = new Order();
            var tmpDate = order.date = DateTime.Now;
            order.number_of_tokens = (int)value;
            switch ((int)value)
            {
                case 1:
                    order.package = "STANDARD";
                    order.price = 50;
                    break;
                case 5:
                    order.package = "GOLD";
                    order.price = 80;
                    break;
                case 10:
                    order.package = "PLATINUM";
                    order.price = 100;
                    break;
            }
            order.status = "WAITING";
            order.user_Id = userId;
            db.Orders.Add(order);
            db.SaveChanges();

            //var ord = db.Orders.Where(i => i.date == tmpDate).FirstOrDefault();
            //var parorderid = ord.Id;

            //return RedirectToAction("AllTokenOrders", orders);
            var basecentili = "http://stage.centili.com/widget/WidgetModule?api=870d5e86ef71dfa2c2570699f9fbf172";
            int p;
            if (btvm.PackageType == "1")
                p = 0;
            else if (btvm.PackageType == "5")
                p = 1;
            else p = 2;

            var ret = "&clientId=" + userId + "&phone=" + btvm.PhoneNumber + "&package=" + p + "&packagelock=true"; /*+ "&orderId=" + parOrderId;*/
            return Redirect(basecentili + ret);
        }

        //public ActionResult BuyTokens()/*String packageType, String phoneNumber)*/
        //{
        //    var userId = User.Identity.GetUserId();
        //    var user   = db.Users.Find(userId);

        //    return View(user);
        //}

        public ManageController()
        {
        }

        public ManageController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        //
        // GET: /Manage/Index
        public async Task<ActionResult> Index(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                  message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
                : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
                : message == ManageMessageId.SetTwoFactorSuccess ? "Your two-factor authentication provider has been set."
                : message == ManageMessageId.Error ? "An error has occurred."
                : message == ManageMessageId.AddPhoneSuccess ? "Your phone number was added."
                : message == ManageMessageId.RemovePhoneSuccess ? "Your phone number was removed."
                : "";

            var userId = User.Identity.GetUserId();
            var model = new IndexViewModel
            {
                //novo
                Name = UserManager.FindById(User.Identity.GetUserId()).Name,
                Surname = UserManager.FindById(User.Identity.GetUserId()).Surname,
                Email = UserManager.FindById(User.Identity.GetUserId()).Email,
                Credit = UserManager.FindById(User.Identity.GetUserId()).Credit,
                //end novo
                HasPassword = HasPassword(),
                PhoneNumber = await UserManager.GetPhoneNumberAsync(userId),
                TwoFactor = await UserManager.GetTwoFactorEnabledAsync(userId),
                Logins = await UserManager.GetLoginsAsync(userId),
                BrowserRemembered = await AuthenticationManager.TwoFactorBrowserRememberedAsync(userId)
            };
            return View(model);
        }

        //
        // POST: /Manage/RemoveLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveLogin(string loginProvider, string providerKey)
        {
            ManageMessageId? message;
            var result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId(), new UserLoginInfo(loginProvider, providerKey));
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                message = ManageMessageId.RemoveLoginSuccess;
            }
            else
            {
                message = ManageMessageId.Error;
            }
            return RedirectToAction("ManageLogins", new { Message = message });
        }

        //
        // GET: /Manage/ChangeDetails
        public ActionResult ChangeDetails()
        {
            var userId = User.Identity.GetUserId();
            var user = db.Users.Find(userId);
            var model = new ChangeDetailsViewModel();

            model.Username = user.UserName;
            model.Name = user.Name;
            model.Surname = user.Surname;
            model.Email = user.Email;
            return View(model);
        }

        //
        // POST: /Manage/ChangeDetails
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeDetails(ChangeDetailsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = User.Identity.GetUserId();
            var user = db.Users.Find(userId);

            user.Name = model.Name;
            user.Surname = model.Surname;
            user.Email = model.Email;
            user.UserName = model.Username;

            db.SaveChanges();

            return RedirectToAction("Index", "Manage");
        }

        //
        // GET: /Manage/ChangePassword
        public ActionResult ChangePassword()
        {
            return View();
        }

        //
        // POST: /Manage/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                return RedirectToAction("Index", new { Message = ManageMessageId.ChangePasswordSuccess });
            }
            AddErrors(result);
            return View(model);
        }

        //
        // GET: /Manage/SetPassword
        public ActionResult SetPassword()
        {
            return View();
        }

        //
        // POST: /Manage/SetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SetPassword(SetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);
                if (result.Succeeded)
                {
                    var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                    if (user != null)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                    }
                    return RedirectToAction("Index", new { Message = ManageMessageId.SetPasswordSuccess });
                }
                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Manage/ManageLogins
        public async Task<ActionResult> ManageLogins(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
                : message == ManageMessageId.Error ? "An error has occurred."
                : "";
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return View("Error");
            }
            var userLogins = await UserManager.GetLoginsAsync(User.Identity.GetUserId());
            var otherLogins = AuthenticationManager.GetExternalAuthenticationTypes().Where(auth => userLogins.All(ul => auth.AuthenticationType != ul.LoginProvider)).ToList();
            ViewBag.ShowRemoveButton = user.PasswordHash != null || userLogins.Count > 1;
            return View(new ManageLoginsViewModel
            {
                CurrentLogins = userLogins,
                OtherLogins = otherLogins
            });
        }

        //
        // POST: /Manage/LinkLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LinkLogin(string provider)
        {
            // Request a redirect to the external login provider to link a login for the current user
            return new AccountController.ChallengeResult(provider, Url.Action("LinkLoginCallback", "Manage"), User.Identity.GetUserId());
        }

        //
        // GET: /Manage/LinkLoginCallback
        public async Task<ActionResult> LinkLoginCallback()
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(XsrfKey, User.Identity.GetUserId());
            if (loginInfo == null)
            {
                return RedirectToAction("ManageLogins", new { Message = ManageMessageId.Error });
            }
            var result = await UserManager.AddLoginAsync(User.Identity.GetUserId(), loginInfo.Login);
            return result.Succeeded ? RedirectToAction("ManageLogins") : RedirectToAction("ManageLogins", new { Message = ManageMessageId.Error });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                _userManager = null;
            }

            base.Dispose(disposing);
        }

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private bool HasPassword()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        }

        private bool HasPhoneNumber()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                return user.PhoneNumber != null;
            }
            return false;
        }

        public enum ManageMessageId
        {
            AddPhoneSuccess,
            ChangePasswordSuccess,
            SetTwoFactorSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            RemovePhoneSuccess,
            Error
        }

        #endregion
    }
}