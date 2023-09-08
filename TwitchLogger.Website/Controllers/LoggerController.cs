using Microsoft.AspNetCore.Mvc;

namespace TwitchLogger.Website.Controllers
{
    public class LoggerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
