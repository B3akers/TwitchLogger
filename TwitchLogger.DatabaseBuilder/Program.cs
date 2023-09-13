using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.IO.Compression;
using TwitchLogger.DTO;
using TwitchLogger.SimpleGraphQL;
using System.Threading.Channels;
using System.Collections.Generic;

public class Pair<T1, T2>
{
    public T1 First { get; set; }
    public T2 Second { get; set; }
}

public class Pair3<T1, T2, T3>
{
    public T1 First { get; set; }
    public T2 Second { get; set; }
    public T3 Third { get; set; }
}

class Program
{
    private static IMongoDatabase _mongoDatabase;
    private static IMongoCollection<ChannelDTO> _channelsCollection;
    private static IMongoCollection<TwitchAccountDTO> _twitchAccountsCollection;
    private static IMongoCollection<TwitchUserMessageTime> _twitchUsersMessageTimeCollection;
    private static IMongoCollection<TwitchUserSubscriptionDTO> _twitchUserSubscriptionCollection;
    private static IMongoCollection<TwitchUserStatDTO> _twitchUserStatsCollection;
    private static ConcurrentDictionary<string, IMongoCollection<TwitchWordUserStatDTO>> _twitchWordUserStatCollections;
    private static ConcurrentDictionary<string, IMongoCollection<TwitchWordStatDTO>> _twitchWordStatCollections;

    private static Collation ignoreCaseCollation = new Collation("en", strength: CollationStrength.Secondary);

    static async Task<Tuple<string, long>> GetChannelInfoFromFile(string file)
    {
        Tuple<string, long> result = null;
        var isGz = file.EndsWith(".gz");

        using (FileStream fileStream = File.OpenRead(file))
        {
            Stream stream = fileStream;

            if (isGz)
                stream = new GZipStream(fileStream, CompressionMode.Decompress, true);

            using (StreamReader streamReader = new StreamReader(stream, System.Text.Encoding.UTF8))
            {
                while (true)
                {
                    var command = await streamReader.ReadLineAsync();
                    if (command == null) break;
                    var commandArgs = command.Split(' ');
                    Dictionary<string, string> senderInfos = new();
                    var messageInfos = commandArgs[0].Substring(1).Split(';');
                    foreach (var info in messageInfos)
                    {
                        var splitInfo = info.Split('=');
                        senderInfos[splitInfo[0]] = splitInfo[1];
                    }
                    if (commandArgs[2] == "PRIVMSG" && senderInfos.ContainsKey("tmi-sent-ts"))
                    {
                        string channelName = commandArgs[3].Trim().Substring(1);
                        var trackingDate = long.Parse(senderInfos["tmi-sent-ts"]) / 1000;

                        result = new Tuple<string, long>(channelName, trackingDate);
                        break;
                    }
                }
            }

            if (isGz)
                stream.Dispose();
        }

        return result;
    }
    static bool IsLegalUnicode(string str)
    {
        for (int i = 0; i < str.Length; i++)
        {
            var uc = char.GetUnicodeCategory(str, i);

            if (uc == UnicodeCategory.Surrogate)
            {
                // Unpaired surrogate, like  "😵"[0] + "A" or  "😵"[1] + "A"
                return false;
            }
            else if (uc == UnicodeCategory.OtherNotAssigned)
            {
                // \uF000 or \U00030000
                return false;
            }

            // Correct high-low surrogate, we must skip the low surrogate
            // (it is correct because otherwise it would have been a 
            // UnicodeCategory.Surrogate)
            if (char.IsHighSurrogate(str, i))
            {
                i++;
            }
        }

        return true;
    }

    static HashSet<string> GetWords(string message, out int initLen)
    {
        HashSet<string> list = new(StringComparer.InvariantCultureIgnoreCase);
        var splited = message.Split(' ');

        foreach (var word in splited)
        {
            if (!string.IsNullOrEmpty(word) && IsLegalUnicode(word))
            {
                list.Add(word);
            }
        }

        initLen = list.Count;
        return list;
    }

    static object[] locksUpdate = new object[10]
    {
        new object(),
        new object(),
        new object(),
        new object(),
        new object(),
        new object(),
        new object(),
        new object(),
        new object(),
        new object()
    };

    static async Task ProcessLogFile(string file)
    {
        var isGz = file.EndsWith(".gz");

        var filterTwitchAccount = Builders<TwitchAccountDTO>.Filter;
        var filterTwitchUserMessageTime = Builders<TwitchUserMessageTime>.Filter;
        var filterUserStat = Builders<TwitchUserStatDTO>.Filter;
        var filterWordUserStat = Builders<TwitchWordUserStatDTO>.Filter;
        var filterWordStat = Builders<TwitchWordStatDTO>.Filter;

        Dictionary<string, UpdateOneModel<TwitchAccountDTO>> twitchAccounts = new Dictionary<string, UpdateOneModel<TwitchAccountDTO>>();
        Dictionary<string, HashSet<string>> twitchUsersMessageTimes = new Dictionary<string, HashSet<string>>();
        Dictionary<string, Pair<long, ulong>> channelsUpdate = new();
        Dictionary<string, Dictionary<string, Pair3<ulong, ulong, ulong>>> userStatsUpdate = new();
        Dictionary<string, Dictionary<string, Dictionary<string, ulong>>> userWordsUpdate = new();
        Dictionary<string, Dictionary<string, ulong>> wordsUpdate = new();
        List<TwitchUserSubscriptionDTO> channelSubsUpdate = new();

        using (FileStream fileStream = File.OpenRead(file))
        {
            Stream stream = fileStream;

            if (isGz)
                stream = new GZipStream(fileStream, CompressionMode.Decompress, true);

            using (StreamReader streamReader = new StreamReader(stream, System.Text.Encoding.UTF8))
            {
                while (true)
                {
                    var command = await streamReader.ReadLineAsync();
                    if (command == null) break;

                    var commandArgs = command.Split(' ');

                    Dictionary<string, string> senderInfos = new();

                    var messageInfos = commandArgs[0].Substring(1).Split(';');
                    foreach (var info in messageInfos)
                    {
                        var splitInfo = info.Split('=');
                        senderInfos[splitInfo[0]] = splitInfo[1];
                    }

                    if (senderInfos.ContainsKey("target-user-id") && !senderInfos.ContainsKey("user-id"))
                        senderInfos["user-id"] = senderInfos["target-user-id"];

                    if (commandArgs[2] == "PRIVMSG" || commandArgs[2] == "USERNOTICE")
                    {
                        string channel = commandArgs[3].Trim();
                        string message = commandArgs.Length > 4 ? string.Join(' ', commandArgs.Skip(4)).Substring(1) : "";
                        if (!string.IsNullOrEmpty(message) && message[0] == '\x01')
                        {
                            var msgStartIndex = message.IndexOf(' ');
                            if (msgStartIndex != -1)
                                message = message.Substring(msgStartIndex + 1, message.Length - msgStartIndex - 2);
                        }

                        if (commandArgs[2] == "PRIVMSG")
                            senderInfos["user-login"] = commandArgs[1].Substring(1, commandArgs[1].IndexOf('!') - 1);
                        else
                            senderInfos["user-login"] = senderInfos["login"];

                        if (senderInfos.ContainsKey("room-id") && senderInfos.ContainsKey("user-id"))
                        {
                            var unixCurrentDate = long.Parse(senderInfos["tmi-sent-ts"]) / 1000;
                            var currentDate = DateTimeOffset.FromUnixTimeSeconds(unixCurrentDate);
                            var userId = senderInfos["user-id"];
                            var userLogin = senderInfos["user-login"];
                            var userDisplayname = senderInfos["display-name"];
                            var roomId = senderInfos["room-id"];

                            var keyAccount = userId + userLogin;
                            if (!twitchAccounts.ContainsKey(keyAccount))
                            {
                                twitchAccounts.Add(keyAccount, new UpdateOneModel<TwitchAccountDTO>(filterTwitchAccount.Eq(x => x.UserId, userId) & filterTwitchAccount.Eq(x => x.Login, userLogin), Builders<TwitchAccountDTO>.Update.Set(x => x.DisplayName, userDisplayname).SetOnInsert(x => x.RecordInsertTime, unixCurrentDate))
                                {
                                    IsUpsert = true
                                });
                            }

                            if (commandArgs[2] == "PRIVMSG")
                            {
                                var keyUserRoom = $"{userId}?{roomId}";
                                //User logs
                                {
                                    if (!twitchUsersMessageTimes.TryGetValue(keyUserRoom, out var list))
                                    {
                                        list = new HashSet<string>();
                                        twitchUsersMessageTimes.Add(keyUserRoom, list);
                                    }
                                    list.Add(currentDate.ToString("yyyy-MM"));
                                }

                                //Channel stats
                                {
                                    if (!channelsUpdate.TryGetValue(roomId, out var list))
                                    {
                                        list = new Pair<long, ulong>() { First = 0, Second = 0 };
                                        channelsUpdate.Add(roomId, list);
                                    }

                                    if (unixCurrentDate > list.First)
                                        list.First = unixCurrentDate;

                                    list.Second++;
                                }

                                var wordsMessage = GetWords(message, out var initLen);

                                //User stats
                                {
                                    {
                                        var key = $"{userId}?0";
                                        if (!userStatsUpdate.TryGetValue(roomId, out var secondDict))
                                        {
                                            secondDict = new Dictionary<string, Pair3<ulong, ulong, ulong>>();
                                            userStatsUpdate.Add(roomId, secondDict);
                                        }

                                        if (!secondDict.TryGetValue(key, out var list))
                                        {
                                            list = new Pair3<ulong, ulong, ulong> { First = 0, Second = 0, Third = 0 };
                                            secondDict.Add(key, list);
                                        }

                                        list.First++;
                                        list.Second += (ulong)initLen;
                                        list.Third += (ulong)message.Length;
                                    }

                                    {
                                        var key = $"{userId}?{currentDate.ToString("yyyy")}";
                                        if (!userStatsUpdate.TryGetValue(roomId, out var secondDict))
                                        {
                                            secondDict = new Dictionary<string, Pair3<ulong, ulong, ulong>>();
                                            userStatsUpdate.Add(roomId, secondDict);
                                        }

                                        if (!secondDict.TryGetValue(key, out var list))
                                        {
                                            list = new Pair3<ulong, ulong, ulong> { First = 0, Second = 0, Third = 0 };
                                            secondDict.Add(key, list);
                                        }

                                        list.First++;
                                        list.Second += (ulong)initLen;
                                        list.Third += (ulong)message.Length;
                                    }
                                }

                                //User word stats
                                {
                                    if (!userWordsUpdate.TryGetValue(roomId, out var secondDict))
                                    {
                                        secondDict = new Dictionary<string, Dictionary<string, ulong>>();
                                        userWordsUpdate.Add(roomId, secondDict);
                                    }

                                    var keyAll = $"{userId}?0";
                                    var keyYear = $"{userId}?{currentDate.ToString("yyyy")}";

                                    if (!secondDict.TryGetValue(keyAll, out var listAll))
                                    {
                                        listAll = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
                                        secondDict.Add(keyAll, listAll);
                                    }

                                    if (!secondDict.TryGetValue(keyYear, out var listYear))
                                    {
                                        listYear = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
                                        secondDict.Add(keyYear, listYear);
                                    }

                                    var keyChannelAll = $"{roomId}?0";
                                    var keyChannelYear = $"{roomId}?{currentDate.ToString("yyyy")}";

                                    if (!wordsUpdate.TryGetValue(keyChannelAll, out var listChannelAll))
                                    {
                                        listChannelAll = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
                                        wordsUpdate.Add(keyChannelAll, listChannelAll);
                                    }

                                    if (!wordsUpdate.TryGetValue(keyChannelYear, out var listChannelYear))
                                    {
                                        listChannelYear = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
                                        wordsUpdate.Add(keyChannelYear, listChannelYear);
                                    }

                                    foreach (var word in wordsMessage)
                                    {
                                        {
                                            listAll.TryGetValue(word, out var value);
                                            listAll[word] = value + 1;
                                        }

                                        {
                                            listYear.TryGetValue(word, out var value);
                                            listYear[word] = value + 1;
                                        }

                                        {
                                            listChannelAll.TryGetValue(word, out var value);
                                            listChannelAll[word] = value + 1;
                                        }

                                        {
                                            listChannelYear.TryGetValue(word, out var value);
                                            listChannelYear[word] = value + 1;
                                        }
                                    }
                                }
                            }
                            else if (commandArgs[2] == "USERNOTICE")
                            {
                                senderInfos.TryGetValue("msg-id", out string msgId);

                                if (msgId == "sub" || msgId == "resub")
                                {
                                    senderInfos.TryGetValue("msg-param-cumulative-months", out string monthsStr);
                                    senderInfos.TryGetValue("msg-param-sub-plan", out string subPlan);
                                    int.TryParse(monthsStr ?? "1", out int months);

                                    channelSubsUpdate.Add(new TwitchUserSubscriptionDTO() { RoomId = roomId, UserId = userId, RecipientUserId = userId, SubPlan = subPlan ?? "", SubMessage = message, CumulativeMonths = months, Timestamp = unixCurrentDate });
                                }
                                else if (msgId == "subgift")
                                {
                                    if (senderInfos.TryGetValue("msg-param-recipient-id", out string recipientId))
                                    {
                                        senderInfos.TryGetValue("msg-param-sub-plan", out string subPlan);
                                        senderInfos.TryGetValue("msg-param-months", out string monthsStr);
                                        int.TryParse(monthsStr ?? "1", out int months);

                                        channelSubsUpdate.Add(new TwitchUserSubscriptionDTO() { RoomId = roomId, UserId = userId, RecipientUserId = recipientId, SubPlan = subPlan ?? "", SubMessage = message, CumulativeMonths = months, Timestamp = unixCurrentDate });
                                    }
                                }
                            }
                        }
                    }
                }

                if (isGz)
                    stream.Dispose();
            }
        }

        lock (locksUpdate[0])
        {
            if (channelSubsUpdate.Count > 0)
                _twitchUserSubscriptionCollection.InsertMany(channelSubsUpdate);
        }

        foreach (var wordStat in wordsUpdate)
        {
            var data = wordStat.Key.Split('?');
            var collection = _twitchWordStatCollections[data[0]];
            List<UpdateOneModel<TwitchWordStatDTO>> wordStats = new();

            foreach (var word in wordStat.Value)
            {
                wordStats.Add(new UpdateOneModel<TwitchWordStatDTO>(filterWordStat.Eq(x => x.Word, word.Key) & filterWordStat.Eq(x => x.Year, int.Parse(data[1])), Builders<TwitchWordStatDTO>.Update.Inc(x => x.Count, word.Value)) { Collation = ignoreCaseCollation, IsUpsert = true });
            }
            lock (locksUpdate[1])
            {
                collection.BulkWrite(wordStats);
            }
        }

        foreach (var userWordStat in userWordsUpdate)
        {
            var collection = _twitchWordUserStatCollections[userWordStat.Key];

            List<UpdateOneModel<TwitchWordUserStatDTO>> userWordStats = new();
            foreach (var item in userWordStat.Value)
            {
                var data = item.Key.Split('?');
                foreach (var word in item.Value)
                {
                    userWordStats.Add(new UpdateOneModel<TwitchWordUserStatDTO>(filterWordUserStat.Eq(x => x.UserId, data[0]) & filterWordUserStat.Eq(x => x.Word, word.Key) & filterWordUserStat.Eq(x => x.Year, int.Parse(data[1])), Builders<TwitchWordUserStatDTO>.Update.Inc(x => x.Count, word.Value)) { Collation = ignoreCaseCollation, IsUpsert = true });
                }
            }
            lock (locksUpdate[2])
            {
                collection.BulkWrite(userWordStats);
            }
        }

        foreach (var userStat in userStatsUpdate)
        {
            List<UpdateOneModel<TwitchUserStatDTO>> usersStatsUpdates = new();
            foreach (var item in userStat.Value)
            {
                var data = item.Key.Split('?');
                usersStatsUpdates.Add(new UpdateOneModel<TwitchUserStatDTO>(filterUserStat.Eq(x => x.RoomId, userStat.Key) & filterUserStat.Eq(x => x.UserId, data[0]) & filterUserStat.Eq(x => x.Year, int.Parse(data[1])),
                    Builders<TwitchUserStatDTO>.Update.Inc(x => x.Messages, item.Value.First).Inc(x => x.Words, item.Value.Second).Inc(x => x.Chars, item.Value.Third))
                { IsUpsert = true });
            }
            lock (locksUpdate[3])
            {
                _twitchUserStatsCollection.BulkWrite(usersStatsUpdates);
            }
        }

        List<UpdateOneModel<TwitchUserMessageTime>> messageTimes = new();
        foreach (var userMessageTime in twitchUsersMessageTimes)
        {
            var data = userMessageTime.Key.Split('?');
            messageTimes.Add(new UpdateOneModel<TwitchUserMessageTime>(filterTwitchUserMessageTime.Eq(x => x.UserId, data[0]) & filterTwitchUserMessageTime.Eq(x => x.RoomId, data[1]), Builders<TwitchUserMessageTime>.Update.AddToSetEach(x => x.MessageTimes, userMessageTime.Value))
            {
                IsUpsert = true
            });
        }

        lock (locksUpdate[4])
        {
            if (messageTimes.Count > 0)
                _twitchUsersMessageTimeCollection.BulkWrite(messageTimes);
        }

        List<UpdateOneModel<ChannelDTO>> channelsUpdateBulk = new();
        foreach (var channel in channelsUpdate)
        {
            channelsUpdateBulk.Add(new UpdateOneModel<ChannelDTO>(Builders<ChannelDTO>.Filter.Eq(x => x.UserId, channel.Key), Builders<ChannelDTO>.Update.Inc(x => x.MessageCount, channel.Value.Second)));
            channelsUpdateBulk.Add(new UpdateOneModel<ChannelDTO>(Builders<ChannelDTO>.Filter.Eq(x => x.UserId, channel.Key), Builders<ChannelDTO>.Update.Max(x => x.MessageLastDate, channel.Value.First)));
        }
        lock (locksUpdate[5])
        {
            if (channelsUpdateBulk.Count > 0)
                _channelsCollection.BulkWrite(channelsUpdateBulk);
        }
      
        lock (locksUpdate[6])
        {
            if (twitchAccounts.Values.Count > 0)
                _twitchAccountsCollection.BulkWrite(twitchAccounts.Values);
        }
    }

    static async Task CreateIndexesForChannel(string channelId)
    {
        {
            var collection = _mongoDatabase.GetCollection<TwitchWordUserStatDTO>($"twitch_word_user_stat_{channelId}");

            await collection.Indexes.CreateManyAsync(new[]
            {
                    new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.Word).Ascending(x => x.Year), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) }),
                    new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.Count).Ascending(x => x.Year)),
                    new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.Word).Descending(x => x.Count).Ascending(x => x.Year), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) })
            });

            _twitchWordUserStatCollections[channelId] = collection;
        }

        {
            var collection = _mongoDatabase.GetCollection<TwitchWordStatDTO>($"twitch_word_stat_{channelId}");

            await collection.Indexes.CreateManyAsync(new[]
            {
                    new CreateIndexModel<TwitchWordStatDTO>(Builders<TwitchWordStatDTO>.IndexKeys.Ascending(x => x.Word).Ascending(x => x.Year), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary), Unique = true }),
                    new CreateIndexModel<TwitchWordStatDTO>(Builders<TwitchWordStatDTO>.IndexKeys.Ascending(x => x.Year).Descending(x => x.Count))
            });

            _twitchWordStatCollections[channelId] = collection;
        }
    }

    static async Task Main(string[] args)
    {
        JObject settings = JObject.Parse(System.Text.Encoding.UTF8.GetString(TwitchLogger.DatabaseBuilder.Properties.Resources.appsettings));
        var connectionString = settings["mongo"]["connectionString"].ToString();
        var databaseName = settings["mongo"]["databaseName"].ToString();

        Console.WriteLine("Cleanup...");

        var _client = new MongoClient(connectionString);
        await _client.DropDatabaseAsync(databaseName);

        _mongoDatabase = _client.GetDatabase(databaseName);

        _channelsCollection = _mongoDatabase.GetCollection<ChannelDTO>("channels");
        _twitchAccountsCollection = _mongoDatabase.GetCollection<TwitchAccountDTO>("twitch_accounts");
        _twitchUsersMessageTimeCollection = _mongoDatabase.GetCollection<TwitchUserMessageTime>("twitch_users_message_time");
        _twitchUserSubscriptionCollection = _mongoDatabase.GetCollection<TwitchUserSubscriptionDTO>("twitch_user_subscriptions");
        _twitchUserStatsCollection = _mongoDatabase.GetCollection<TwitchUserStatDTO>("twitch_user_stats");

        _twitchWordUserStatCollections = new();
        _twitchWordStatCollections = new();

        Console.WriteLine("Starting...");

        //Create indexes
        //
        {
            await _channelsCollection.Indexes.CreateOneAsync(new CreateIndexModel<ChannelDTO>(Builders<ChannelDTO>.IndexKeys.Ascending(x => x.UserId), new CreateIndexOptions() { Unique = true }));

            await _twitchAccountsCollection.Indexes.CreateOneAsync(new CreateIndexModel<TwitchAccountDTO>(Builders<TwitchAccountDTO>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.Login), new CreateIndexOptions() { Unique = true }));
            await _twitchAccountsCollection.Indexes.CreateOneAsync(new CreateIndexModel<TwitchAccountDTO>(Builders<TwitchAccountDTO>.IndexKeys.Ascending(x => x.Login).Descending(x => x.RecordInsertTime), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) }));

            await _twitchUsersMessageTimeCollection.Indexes.CreateOneAsync(new CreateIndexModel<TwitchUserMessageTime>(Builders<TwitchUserMessageTime>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.RoomId), new CreateIndexOptions() { Unique = true }));

            await _twitchUserSubscriptionCollection.Indexes.CreateOneAsync(new CreateIndexModel<TwitchUserSubscriptionDTO>(Builders<TwitchUserSubscriptionDTO>.IndexKeys.Ascending(x => x.RoomId).Descending(x => x.Timestamp)));

            await _twitchUserStatsCollection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<TwitchUserStatDTO>(Builders<TwitchUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.UserId).Ascending(x => x.Year), new CreateIndexOptions() { Unique = true }),
                new CreateIndexModel<TwitchUserStatDTO>(Builders<TwitchUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Descending(x => x.Messages).Ascending(x => x.Year)),
            });
        }

        var logsPath = settings["Logs"]["DataLogDirectory"].ToString();

        if (!Directory.Exists(logsPath))
            return;


        List<Task> currentTasks = new List<Task>();

        foreach (var channel in Directory.GetDirectories(logsPath))
        {
            List<Tuple<DateTimeOffset, string>> logsFiles = new();
            foreach (var year in Directory.GetDirectories(Path.Combine(channel, "channel")))
            {
                foreach (var month in Directory.GetDirectories(year))
                {
                    foreach (var day in Directory.GetFiles(month))
                    {
                        var date = DateTimeOffset.ParseExact($"{Path.GetFileName(year)}-{Path.GetFileName(month)}-{Path.GetFileName(day).Replace(".gz", "")}", "yyyy-MM-dd", CultureInfo.InvariantCulture.DateTimeFormat);
                        logsFiles.Add(new Tuple<DateTimeOffset, string>(date, day));
                    }
                }
            }

            logsFiles = logsFiles.OrderBy(x => x.Item1).ToList();
            if (logsFiles.Count == 0)
                continue;

            var result = await GetChannelInfoFromFile(logsFiles[0].Item2);
            if (result == null)
                continue;

            TwitchUser channelData = null;
            var channelId = Path.GetFileName(channel);
            try
            {
                channelData = await TwitchGraphQL.GetUserInfoById(channelId);
            }
            catch
            {

            }

            if (channelData == null)
            {
                channelData = new TwitchUser();
                channelData.Id = channelId;
                channelData.ProfileImageURL = string.Empty;
                channelData.Login = result.Item1;
                channelData.DisplayName = result.Item1;
            }

            await CreateIndexesForChannel(channelData.Id);
            await _channelsCollection.InsertOneAsync(new ChannelDTO() { UserId = channelData.Id, Login = channelData.Login, DisplayName = channelData.DisplayName, LogoUrl = channelData.ProfileImageURL, MessageCount = 0, MessageLastDate = 0, StartTrackingDate = result.Item2 });

            foreach (var logfile in logsFiles)
            {
                var file = logfile.Item2;
                currentTasks.Add(Task.Run(() => ProcessLogFile(file)));
            }
            await Task.WhenAll(currentTasks);
        }

        Console.WriteLine("Imported!");
    }
}