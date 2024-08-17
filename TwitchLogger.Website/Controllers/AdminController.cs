using Microsoft.AspNetCore.Mvc;
using System.Threading.Channels;
using TwitchLogger.SimpleGraphQL;
using TwitchLogger.Website.ActionFilters;
using TwitchLogger.Website.Interfaces;
using TwitchLogger.Website.Models;

namespace TwitchLogger.Website.Controllers
{
    [TypeFilter(typeof(AdminActionFilter))]
    public class AdminController : Controller
    {
        private readonly IChannelRepository _channelRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IOptChannelRepository _optChannelRepository;

        public AdminController(IChannelRepository channelRepository, IAccountRepository accountRepository, IOptChannelRepository optChannelRepository)
        {
            _channelRepository = channelRepository;
            _accountRepository = accountRepository;
            _optChannelRepository = optChannelRepository;
        }

        public IActionResult Index()
        {
            return View("Channels");
        }

        public IActionResult Accounts()
        {
            return View();
        }

        public IActionResult Channels()
        {
            return View();
        }

        public IActionResult OptChannels()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAccount([FromBody] AddAccountModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            var account = HttpContext.Items["userAccount"] as DTO.AccountDTO;
            if (!account.IsAdmin)
                return Json(new { error = "server_error" });

            if (await _accountRepository.GetAccountByLogin(model.Login) != null)
                return Json(new { error = "account_already_exists" });

            var createdAccount = await _accountRepository.CreateAccount(model.Login, model.Password, model.IsAdmin, model.IsModerator);

            return Json(new { success = "account_created", accountId = createdAccount.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount([FromBody] IdModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            var account = HttpContext.Items["userAccount"] as DTO.AccountDTO;
            if (!account.IsAdmin)
                return Json(new { error = "server_error" });

            if (model.Id == account.Id)
                return Json(new { error = "invalid_model" });

            await _accountRepository.DeleteAccount(model.Id);

            return Json(new { success = "account_deleted" });
        }

        [HttpGet]
        public async Task<IActionResult> GetAccounts()
        {
            var account = HttpContext.Items["userAccount"] as DTO.AccountDTO;
            if (!account.IsAdmin)
                return Json(new { error = "server_error" });

            var accounts = await _accountRepository.GetAccounts();

            return Json(new { data = accounts.Select(x => new { x.Id, x.Login, x.CreationTime, x.IsModerator, x.IsAdmin }) });
        }

        [HttpGet]
        public async Task<IActionResult> GetOptChannels()
        {
            var account = HttpContext.Items["userAccount"] as DTO.AccountDTO;
            if (!account.IsAdmin)
                return Json(new { error = "server_error" });

            var accounts = await _optChannelRepository.GetChannels();

            return Json(new { data = accounts });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOptChannel([FromBody] IdModel model)
        {
            var account = HttpContext.Items["userAccount"] as DTO.AccountDTO;
            if (!account.IsAdmin)
                return Json(new { error = "server_error" });

            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            await _optChannelRepository.DeleteChannel(model.Id);

            return Json(new { success = "channel_deleted" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOptChannel([FromBody] ChannelNameModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            var account = HttpContext.Items["userAccount"] as DTO.AccountDTO;
            if (!account.IsAdmin)
                return Json(new { error = "server_error" });

            var userId = await TwitchGraphQL.GetUserID(model.Login);
            if (string.IsNullOrEmpty(userId))
                return Json(new { error = "channel_not_found" });

            var channel = await _optChannelRepository.AddChannelByUserId(userId);
            if (channel == null)
                return Json(new { error = "channel_not_found" });

            return Json(new { success = "channel_added", channelId = channel.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChannel([FromBody] IdModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            await _channelRepository.DeleteChannel(model.Id);

            return Json(new { success = "channel_deleted" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddChannel([FromBody] ChannelNameModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            var userId = await TwitchGraphQL.GetUserID(model.Login);
            if (string.IsNullOrEmpty(userId))
                return Json(new { error = "channel_not_found" });

            if (await _channelRepository.GetChannelByUserId(userId) != null)
                return Json(new { error = "channel_already_added" });

            var channel = await _channelRepository.AddChannelByUserId(userId);
            if (channel == null)
                return Json(new { error = "channel_not_found" });

            return Json(new { success = "channel_added", channelId = channel.Id });
        }
    }
}
