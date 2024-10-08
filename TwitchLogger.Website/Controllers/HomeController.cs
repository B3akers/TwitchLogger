﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using System.Diagnostics;
using System.Threading.Channels;
using TwitchLogger.SimpleGraphQL;
using TwitchLogger.Website.Interfaces;
using TwitchLogger.Website.Models;
using TwitchLogger.Website.Services;

namespace TwitchLogger.Website.Controllers
{
    public class HomeController : Controller
    {
        private readonly IUserAuthentication _userAuthentication;
        private readonly IChannelRepository _channelRepository;
        private readonly IChannelStatsRepository _channelStatsRepository;
        private readonly ITwitchAccountRepository _twitchAccountRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly IOptChannelRepository _optChannelRepository;
        private readonly DatabaseService _databaseService;
        private readonly string _dataLogDirectory;

        public HomeController(IConfiguration configuration,
            IChannelRepository channelRepository,
            IChannelStatsRepository channelStatsRepository,
            ITwitchAccountRepository twitchAccountRepository,
            IMemoryCache memoryCache,
            IOptChannelRepository optChannelRepository,
            DatabaseService databaseService,
            IUserAuthentication userAuthentication)
        {
            _dataLogDirectory = configuration["Logs:DataLogDirectory"];
            _channelRepository = channelRepository;
            _channelStatsRepository = channelStatsRepository;
            _twitchAccountRepository = twitchAccountRepository;
            _memoryCache = memoryCache;
            _databaseService = databaseService;
            _optChannelRepository = optChannelRepository;
            _userAuthentication = userAuthentication;
        }

        public async Task<IActionResult> Index()
        {
            var model = new HomeIndexViewModel();
            model.ChannelsCount = await _channelRepository.GetEstimatedCount();
            model.TwitchUniqueUsersCount = await _twitchAccountRepository.GetEstimatedUniqueCount();

            model.TotalMessagesCount = await _memoryCache.GetOrCreateAsync("totalMessagesChannels", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                return await _channelRepository.GetAllMessagesCount();
            });

            model.DatabaseSize = await _memoryCache.GetOrCreateAsync("databaseStats", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return await _databaseService.GetDatabaseStats();
            });

            if (model.DatabaseSize == null)
                model.DatabaseSize = new Tuple<long, long>(0, 0);

            return View("Index", model);
        }

        [Route("Home/Channel/{id?}")]
        [Route("Channel/{id?}")]
        public async Task<IActionResult> Channel(string id)
        {
            var channel = await _channelRepository.GetChannelByUserId(id);
            if (channel == null)
                return await Index();

            var isOpt = await _memoryCache.GetOrCreateAsync($"opt_{channel.UserId}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
                return await _optChannelRepository.GetChannelByUserId(channel.UserId) != null;
            });

            if (isOpt)
            {
                var user = await _userAuthentication.GetAuthenticatedUser(HttpContext);
                if (user != null)
                    isOpt = false;
            }

            var currentTime = DateTimeOffset.UtcNow;
            var lastMonth = currentTime.AddMonths(-1);

            var viewModel = new ChannelViewModel();
            viewModel.Channel = channel;
            viewModel.IsOpt = isOpt;
            viewModel.Subscriptions = await _channelStatsRepository.GetUniqueSubscriptions(id, lastMonth.ToUnixTimeSeconds(), currentTime.ToUnixTimeSeconds());
            viewModel.TopSubscribers = await _channelStatsRepository.GetTopSubscriptions(id);

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> GetUserLogsTimes([FromBody] GetUserLogsTimesModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            model.User = await _twitchAccountRepository.GetUserIdFromParam(model.User);

            if (string.IsNullOrEmpty(model.User))
                return Json(new { error = "user_not_found" });

            var result = await _channelStatsRepository.GetUserMessageTime(model.Id, model.User);

            return Json(new { data = result?.MessageTimes ?? new List<string>(), userId = model.User, roomId = model.Id });
        }

        [HttpPost]
        public async Task<IActionResult> GetUserStats([FromBody] GetUserStatsModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            var backupUser = model.User;
            model.User = await _twitchAccountRepository.GetUserIdFromParam(model.User);

            if (string.IsNullOrEmpty(model.User))
                return Json(new { user = string.Empty, messages = 0, words = 0, chars = 0 });

            var result = await _channelStatsRepository.GetUserStats(model.Id, model.User, model.Year);
            if (result == null)
                return Json(new { user = string.Empty, messages = 0, words = 0, chars = 0 });

            return Json(new { user = backupUser, result.Messages, result.Words, result.Chars });
        }

        [HttpPost]
        public async Task<IActionResult> GetWordCount([FromBody] GetWordCountModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            var userBackup = string.Empty;

            if (!string.IsNullOrEmpty(model.User))
            {
                userBackup = model.User;
                model.User = await _twitchAccountRepository.GetUserIdFromParam(model.User);
            }

            return Json(new { word = model.Word, user = userBackup, count = await _channelStatsRepository.GetWordCount(model.Id, model.Word, model.User, model.Year) });
        }

        [HttpPost]
        public async Task<IActionResult> GetTopUserWords([FromBody] GetTopUserWordsModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { error = "invalid_model" });

            model.User = await _twitchAccountRepository.GetUserIdFromParam(model.User);

            if (string.IsNullOrEmpty(model.User))
                return Json(new { error = "user_not_found" });

            var data = await _channelStatsRepository.GetTopUserWords(model.Id, model.User, model.Year);

            return Json(new { data });
        }

        [HttpPost]
        public async Task<IActionResult> GetTopStats([FromBody] GetTopWordsModel model)
        {
            var words = await _channelStatsRepository.GetTopWords(model.Id, model.Year);
            var chatters = await _channelStatsRepository.GetTopChatters(model.Id, model.Year);
            var channelEmotes = await _channelStatsRepository.GetTopEmotes(model.Id, model.Year);

            return Json(new { words, chatters, channelEmotes });
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

            var isOpt = await _memoryCache.GetOrCreateAsync($"opt_{id}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
                return await _optChannelRepository.GetChannelByUserId(id) != null;
            });

            if (isOpt)
            {
                var user = await _userAuthentication.GetAuthenticatedUser(HttpContext);
                if (user == null)
                    return BadRequest();
            }

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

            return await _memoryCache.GetOrCreateAsync($"badges_{channelLogin}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
                var result = await TwitchGraphQL.GetChannelBadgesInfo(channelLogin);
                return Json(new { data = result });
            });
        }

        [HttpPost]
        public async Task<IActionResult> GetChannelLogs([FromBody] GetChannelLogsModel model)
        {
            if (string.IsNullOrEmpty(model.Id))
                return NotFound();

            try
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(model.Date);
                return await GetLogs(model.Id, "channel", date.ToString("yyyy"), date.ToString("MM"), date.ToString("dd"));
            }
            catch { }

            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> GetUserLogs([FromBody] GetUserLogsModel model)
        {
            if (string.IsNullOrEmpty(model.Id))
                return NotFound();

            if (string.IsNullOrEmpty(model.User))
                return NotFound();

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