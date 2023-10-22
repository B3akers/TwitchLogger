using Microsoft.AspNetCore.Mvc;
using TwitchLogger.Website.Interfaces;
using TwitchLogger.Website.Models;

namespace TwitchLogger.Website.Controllers
{
    public class LoginHistoryController : Controller
    {
        private readonly ITwitchAccountRepository _twitchAccountRepository;

        public LoginHistoryController(ITwitchAccountRepository twitchAccountRepository)
        {
            _twitchAccountRepository = twitchAccountRepository;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> GetHistory([FromBody] UserLoginModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            model.User = await _twitchAccountRepository.GetUserIdFromParam(model.User);
            if (string.IsNullOrEmpty(model.User))
                return Json(new { error = "user_not_found" });

            return Json(new { data = await _twitchAccountRepository.GetUserTwitchAccounts(model.User) });
        }
    }
}
