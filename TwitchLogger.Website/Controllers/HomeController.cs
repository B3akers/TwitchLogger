using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using System.Diagnostics;
using TwitchLogger.SimpleGraphQL;
using TwitchLogger.Website.Interfaces;
using TwitchLogger.Website.Models;

namespace TwitchLogger.Website.Controllers
{
    public class HomeController : Controller
    {
        private readonly IChannelRepository _channelRepository;
        private readonly IChannelStatsRepository _channelStatsRepository;
        private readonly ITwitchAccountRepository _twitchAccountRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly string _dataLogDirectory;

        public HomeController(IConfiguration configuration, IChannelRepository channelRepository, IChannelStatsRepository channelStatsRepository, ITwitchAccountRepository twitchAccountRepository, IMemoryCache memoryCache)
        {
            _dataLogDirectory = configuration["Logs:DataLogDirectory"];
            _channelRepository = channelRepository;
            _channelStatsRepository = channelStatsRepository;
            _twitchAccountRepository = twitchAccountRepository;
            _memoryCache = memoryCache;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Route("Home/Channel/{id?}")]
        public async Task<IActionResult> Channel(string id)
        {
            var channel = await _channelRepository.GetChannelByUserId(id);
            if (channel == null)
                return View("Index");

            var viewModel = new ChannelViewModel();
            viewModel.Channel = channel;
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> GetUserLogsTimes([FromBody] GetUserLogsTimesModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            if (!model.User.All(char.IsDigit))
            {
                var user = await _twitchAccountRepository.GetTwitchAccountByLogin(model.User);
                if (user == null)
                    return Json(new { error = "user_not_found" });

                model.User = user.UserId;
            }

            var result = await _channelStatsRepository.GetUserMessageTime(model.Id, model.User);

            return Json(new { data = result?.MessageTimes ?? new List<string>(), userId = model.User, roomId = model.Id });
        }

        [HttpPost]
        public async Task<IActionResult> GetTopUserWords([FromBody] GetTopUserWordsModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            if (!model.User.All(char.IsDigit))
            {
                var user = await _twitchAccountRepository.GetTwitchAccountByLogin(model.User);
                if (user == null)
                    return Json(new { error = "user_not_found" });

                model.User = user.UserId;
            }

            var data = await _channelStatsRepository.GetTopUserWords(model.Id, model.User, model.Year);

            return Json(new { data });
        }

        [HttpPost]
        public async Task<IActionResult> GetTopWords([FromBody] GetTopWordsModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            var data = await _channelStatsRepository.GetTopWords(model.Id, model.Year);
            return Json(new { data });
        }

        [HttpPost]
        public async Task<IActionResult> GetTopUsers([FromBody] GetTopUsersModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            var data = await _channelStatsRepository.GetTopUsers(model.Id, model.Word, model.Year);
            var userData = await _twitchAccountRepository.GetTwitchAccounts(data.Select(x => x.UserId));

            return Json(new { data, userData });
        }

        public async Task<IActionResult> GetChannels()
        {
            return Json(new { data = await _channelRepository.GetChannels() });
        }

        [HttpGet]
        [Route("Home/GetLogs/{id}/{type}/{year}/{month}/{detail}")]
        public async Task<IActionResult> GetLogs(string id, string type, string year, string month, string detail)
        {
            if (type != "user" && type != "channel")
                return BadRequest();

            if (!detail.All(char.IsDigit) || !id.All(char.IsDigit) || !year.All(char.IsDigit) || !month.All(char.IsDigit))
                return BadRequest();

            var logFile = Path.Combine(_dataLogDirectory, id, type, year, month, detail);
            var logFileGz = $"{logFile}.gz";

            if (System.IO.File.Exists(logFile))
                return new FileStreamResult(System.IO.File.OpenRead(logFile), "application/octet-stream");
            else if (System.IO.File.Exists(logFileGz))
            {
                Response.Headers.TryAdd("Content-encoding", "gzip");
                return new FileStreamResult(System.IO.File.OpenRead(logFileGz), "application/gzip");
            }
            else if (type == "user")
            {
                if (ulong.TryParse(detail, out var findFileId))
                {
                    var packageLogFile = Path.Combine(_dataLogDirectory, id, type, year, month, "files.fpl");
                    if (System.IO.File.Exists(packageLogFile))
                    {
                        using (var stream = System.IO.File.OpenRead(packageLogFile))
                        {
                            var bufferChunk = new byte[0x18];
                            await stream.ReadAsync(bufferChunk, 0, 4);

                            var itemsCount = BitConverter.ToInt32(bufferChunk, 0);
                            var l = 0;
                            var r = itemsCount - 1;
                            while (l <= r)
                            {
                                var middle = (l + r) / 2;

                                stream.Seek(0x4 + (middle * 0x18), SeekOrigin.Begin);
                                await stream.ReadAsync(bufferChunk, 0, 0x18);

                                var userId = BitConverter.ToUInt64(bufferChunk, 0);

                                if (userId == findFileId)
                                {
                                    var offset = BitConverter.ToInt64(bufferChunk, 0x8);
                                    var fileSize = BitConverter.ToInt64(bufferChunk, 0x10);
                                    var outputBuffer = new byte[fileSize > 81920 ? 81920 : fileSize];

                                    Response.ContentType = "application/gzip";
                                    Response.Headers.TryAdd("Content-encoding", "gzip");
                                    Response.ContentLength = fileSize;
                                    Response.StatusCode = 200;

                                    stream.Seek(offset, SeekOrigin.Begin);

                                    while (true)
                                    {
                                        if (fileSize == 0)
                                            break;

                                        int bytesRead = await stream.ReadAsync(outputBuffer, 0, fileSize > outputBuffer.Length ? outputBuffer.Length : (int)fileSize);

                                        if (bytesRead == 0)
                                            break;

                                        await Response.Body.WriteAsync(outputBuffer, 0, bytesRead);

                                        fileSize -= bytesRead;
                                    }

                                    return new EmptyResult();
                                }

                                if (userId > findFileId)
                                    r = middle - 1;
                                else
                                    l = middle + 1;
                            }
                        }
                    }
                }
            }

            return NotFound();
        }

        [ResponseCache(VaryByQueryKeys = new[] { "*" }, Duration = 86400)]
        public async Task<IActionResult> GetChannelBadges([FromQuery] string channelId)
        {
            var channel = await _channelRepository.GetChannelByUserId(channelId);
            if (channel == null)
                return NotFound();

            var channelLogin = channel.Login;

            return await _memoryCache.GetOrCreateAsync(channelLogin, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
                var result = await TwitchGraphQL.GetChannelBadgesInfo(channelLogin);
                return Json(new { data = result });
            });
        }

        [HttpPost]
        public async Task<IActionResult> GetUserLogs([FromBody] GetUserLogsModel model)
        {
            if (string.IsNullOrEmpty(model.Id))
                return NotFound();

            if (!model.User.All(char.IsDigit))
            {
                var user = await _twitchAccountRepository.GetTwitchAccountByLogin(model.User);
                if (user == null)
                    return NotFound();

                model.User = user.UserId;
            }

            try
            {
                var splitted = model.Date.Split('-');
                if (splitted.Length > 1)
                    return await GetLogs(model.Id, "user", splitted[0], splitted[1], model.User);
            }
            catch { }

            return NotFound();
        }
    }
}