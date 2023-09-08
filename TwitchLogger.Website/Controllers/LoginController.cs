using Microsoft.AspNetCore.Mvc;
using TwitchLogger.Website.ActionFilters;
using TwitchLogger.Website.Interfaces;
using TwitchLogger.Website.Models;

namespace TwitchLogger.Website.Controllers
{
    [TypeFilter(typeof(LoginActionFilter))]
    public class LoginController : Controller
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IUserAuthentication _userAuthentication;

        public LoginController(IAccountRepository accountRepository, IUserAuthentication userAuthentication)
        {
            _accountRepository = accountRepository;
            _userAuthentication = userAuthentication;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AccountLogin([FromBody] LoginAccountModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            var account = await _accountRepository.GetAccountByLogin(model.Login);

            if (account == null || !_userAuthentication.CheckCredentials(account, model.Password))
                return Json(new { error = "invalid_credentials" });

            await _userAuthentication.AuthorizeForUser(HttpContext, account.Id, model.RememberMe);

            return Json(new { success = "login_success" });
        }
    }
}
