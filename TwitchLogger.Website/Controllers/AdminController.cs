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
        public AdminController(IChannelRepository channelRepository)
        {
            _channelRepository = channelRepository;
        }

        public IActionResult Index()
        {
            return View("Channels");
        }

        public IActionResult Channels()
        {
            return View();
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
