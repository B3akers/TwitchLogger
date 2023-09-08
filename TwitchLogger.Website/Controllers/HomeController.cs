using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Controllers
{
    public class HomeController : Controller
    {
        private readonly IChannelRepository _channelRepository;
        public HomeController(IChannelRepository channelRepository)
        {
            _channelRepository = channelRepository;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Route("Home/Channel/{id?}")]
        public IActionResult Channel(string id)
        {
            return View();
        }

        public async Task<IActionResult> GetChannels()
        {
            return Json(new { data = await _channelRepository.GetChannels() });
        }
    }
}