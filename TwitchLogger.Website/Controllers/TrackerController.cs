using Microsoft.AspNetCore.Mvc;

namespace TwitchLogger.Website.Controllers
{
    public class TrackerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
