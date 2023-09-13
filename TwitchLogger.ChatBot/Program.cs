using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;
using TwitchLogger.ChatBot;
using TwitchLogger.ChatBot.TwitchIrcClient;
using TwitchLogger.DTO;
using TwitchLogger.SimpleGraphQL;

class Program
{
    private static CancellationTokenSource cancellationToken;
    private static TwitchIrcBot ircBot;
    private static MongoClient _client;

    private static IMongoCollection<ChannelDTO> _channelsCollection;
    private static IMongoCollection<TwitchAccountDTO> _twitchAccountsCollection;
    private static IMongoCollection<TwitchUserMessageTime> _twitchUsersMessageTimeCollection;
    private static IMongoCollection<TwitchUserSubscriptionDTO> _twitchUserSubscriptionCollection;
    private static IMongoCollection<TwitchUserStatDTO> _twitchUserStatsCollection;
    private static ConcurrentDictionary<string, IMongoCollection<TwitchWordUserStatDTO>> _twitchWordUserStatCollections;
    private static ConcurrentDictionary<string, IMongoCollection<TwitchWordStatDTO>> _twitchWordStatCollections;

    private static Collation ignoreCaseCollation = new Collation("en", strength: CollationStrength.Secondary);

    private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
    {
        if (ircBot != null)
            ircBot.Disconnect();
        cancellationToken.Cancel();

        Console.WriteLine("Exiting...");

        e.Cancel = true;
    }

    private static bool IsLegalUnicode(string str)
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

    private static HashSet<string> GetWords(string message, out int initLen)
    {
        HashSet<string> list = new(StringComparer.InvariantCultureIgnoreCase);
        var splited = message.Split(' ');

        initLen = splited.Length;

        foreach (var word in splited)
        {
            if (!string.IsNullOrEmpty(word) && IsLegalUnicode(word))
            {
                list.Add(word);
            }
        }
        return list;
    }

    private static async Task Irc_OnMessage(object sender, TwitchChatMessage args)
    {
        if (!args.SenderInfo.ContainsKey("room-id") || !args.SenderInfo.ContainsKey("user-id"))
        {
            return;
        }

        try
        {
            var roomId = args.SenderInfo["room-id"];
            var userId = args.SenderInfo["user-id"];

            var currentDate = DateTimeOffset.UtcNow;

            await TwitchHelper.AddLogToFiles(currentDate, roomId, userId, args.Raw);
        }
        catch (Exception es) { Console.WriteLine(es); }

        try
        {
            var currentDate = DateTimeOffset.UtcNow;
            var unixCurrentDate = currentDate.ToUnixTimeSeconds();

            if (args.MessageType == TwitchChatMessageType.PRIVMSG || args.MessageType == TwitchChatMessageType.USERNOTICE)
            {
                var userId = args.SenderInfo["user-id"];
                var userLogin = args.SenderInfo["user-login"];
                var userDisplayname = args.SenderInfo["display-name"];

                await _twitchAccountsCollection.UpdateOneAsync(x => x.UserId == userId && x.Login == userLogin, Builders<TwitchAccountDTO>.Update.Set(x => x.DisplayName, userDisplayname).SetOnInsert(x => x.RecordInsertTime, unixCurrentDate), new UpdateOptions() { IsUpsert = true });
            }

            if (args.MessageType == TwitchChatMessageType.PRIVMSG)
            {
                var roomId = args.SenderInfo["room-id"];
                var userId = args.SenderInfo["user-id"];

                await _twitchUsersMessageTimeCollection.UpdateOneAsync(x => x.UserId == userId && x.RoomId == roomId, Builders<TwitchUserMessageTime>.Update.AddToSet(x => x.MessageTimes, currentDate.ToString("yyyy-MM")), new UpdateOptions() { IsUpsert = true });
                await _channelsCollection.UpdateOneAsync(x => x.UserId == roomId, Builders<ChannelDTO>.Update.Set(x => x.MessageLastDate, unixCurrentDate).Inc(x => x.MessageCount, 1ul));

                if (_twitchWordUserStatCollections.TryGetValue(roomId, out var twitchWordUserStat) && _twitchWordStatCollections.TryGetValue(roomId, out var twitchWordStat))
                {
                    List<UpdateOneModel<TwitchWordUserStatDTO>> wordUserStatUpdates = new List<UpdateOneModel<TwitchWordUserStatDTO>>();
                    List<UpdateOneModel<TwitchWordStatDTO>> wordStatUpdates = new List<UpdateOneModel<TwitchWordStatDTO>>();

                    var wordsMessage = GetWords(args.Message, out var initLen);
                    var filterWordUserStat = Builders<TwitchWordUserStatDTO>.Filter;
                    var filterWordStat = Builders<TwitchWordStatDTO>.Filter;
                    var filterUserStat = Builders<TwitchUserStatDTO>.Filter;
                    var year = int.Parse(currentDate.ToString("yyyy"));

                    foreach (var word in wordsMessage)
                    {
                        wordUserStatUpdates.Add(new UpdateOneModel<TwitchWordUserStatDTO>(filterWordUserStat.Eq(x => x.UserId, userId) & filterWordUserStat.Eq(x => x.Word, word) & filterWordUserStat.Eq(x => x.Year, year), Builders<TwitchWordUserStatDTO>.Update.Inc(x => x.Count, 1ul))
                        {
                            Collation = ignoreCaseCollation,
                            IsUpsert = true
                        });

                        wordUserStatUpdates.Add(new UpdateOneModel<TwitchWordUserStatDTO>(filterWordUserStat.Eq(x => x.UserId, userId) & filterWordUserStat.Eq(x => x.Word, word) & filterWordUserStat.Eq(x => x.Year, 0), Builders<TwitchWordUserStatDTO>.Update.Inc(x => x.Count, 1ul))
                        {
                            Collation = ignoreCaseCollation,
                            IsUpsert = true
                        });

                        wordStatUpdates.Add(new UpdateOneModel<TwitchWordStatDTO>(filterWordStat.Eq(x => x.Word, word) & filterWordStat.Eq(x => x.Year, year), Builders<TwitchWordStatDTO>.Update.Inc(x => x.Count, 1ul))
                        {
                            Collation = ignoreCaseCollation,
                            IsUpsert = true
                        });

                        wordStatUpdates.Add(new UpdateOneModel<TwitchWordStatDTO>(filterWordStat.Eq(x => x.Word, word) & filterWordStat.Eq(x => x.Year, 0), Builders<TwitchWordStatDTO>.Update.Inc(x => x.Count, 1ul))
                        {
                            Collation = ignoreCaseCollation,
                            IsUpsert = true
                        });
                    }

                    var updateUserStatDef = Builders<TwitchUserStatDTO>.Update.Inc(x => x.Messages, 1ul).Inc(x => x.Words, (ulong)initLen).Inc(x => x.Chars, (ulong)args.Message.Length);
                    await _twitchUserStatsCollection.BulkWriteAsync(new[]
                    {
                        new UpdateOneModel<TwitchUserStatDTO>(filterUserStat.Eq(x => x.RoomId, roomId) & filterUserStat.Eq(x => x.UserId, userId) & filterUserStat.Eq(x => x.Year, year), updateUserStatDef)
                        {
                            IsUpsert = true
                        },
                        new UpdateOneModel<TwitchUserStatDTO>(filterUserStat.Eq(x => x.RoomId, roomId) & filterUserStat.Eq(x => x.UserId, userId) & filterUserStat.Eq(x => x.Year, 0), updateUserStatDef)
                        {
                            IsUpsert = true
                        }
                    });

                    if (wordUserStatUpdates.Count > 0)
                        await twitchWordUserStat.BulkWriteAsync(wordUserStatUpdates);

                    if (wordStatUpdates.Count > 0)
                        await twitchWordStat.BulkWriteAsync(wordStatUpdates);
                }
            }
            else if (args.MessageType == TwitchChatMessageType.USERNOTICE)
            {
                var roomId = args.SenderInfo["room-id"];
                var userId = args.SenderInfo["user-id"];

                args.SenderInfo.TryGetValue("msg-id", out string msgId);

                if (msgId == "sub" || msgId == "resub")
                {
                    args.SenderInfo.TryGetValue("msg-param-cumulative-months", out string monthsStr);
                    args.SenderInfo.TryGetValue("msg-param-sub-plan", out string subPlan);
                    int.TryParse(monthsStr ?? "1", out int months);

                    await _twitchUserSubscriptionCollection.InsertOneAsync(new TwitchUserSubscriptionDTO() { RoomId = roomId, UserId = userId, RecipientUserId = userId, SubPlan = subPlan ?? "", SubMessage = args.Message, CumulativeMonths = months, Timestamp = unixCurrentDate });
                }
                else if (msgId == "subgift")
                {
                    if (args.SenderInfo.TryGetValue("msg-param-recipient-id", out string recipientId))
                    {
                        args.SenderInfo.TryGetValue("msg-param-sub-plan", out string subPlan);
                        args.SenderInfo.TryGetValue("msg-param-months", out string monthsStr);
                        int.TryParse(monthsStr ?? "1", out int months);

                        await _twitchUserSubscriptionCollection.InsertOneAsync(new TwitchUserSubscriptionDTO() { RoomId = roomId, UserId = userId, RecipientUserId = recipientId, SubPlan = subPlan ?? "", SubMessage = args.Message, CumulativeMonths = months, Timestamp = unixCurrentDate });
                    }
                }
            }
        }
        catch (Exception es) { Console.WriteLine(es); }
    }

    private static async Task ArchiveLogsLoop()
    {
        try
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                Console.WriteLine("Archive logs...");
                await TwitchHelper.ArchiveLogs();
                Console.WriteLine("Archive done");
                await Task.Delay(3600 * 24 * 1000, cancellationToken.Token);
            }
        }
        catch { }
    }

    static async Task Main(string[] args)
    {
        JObject settings = JObject.Parse(System.Text.Encoding.UTF8.GetString(TwitchLogger.ChatBot.Properties.Resources.appsettings));
        var connectionString = settings["mongo"]["connectionString"].ToString();
        var databaseName = settings["mongo"]["databaseName"].ToString();

        _client = new MongoClient(connectionString);
        var mongoDatabase = _client.GetDatabase(databaseName);

        _channelsCollection = mongoDatabase.GetCollection<ChannelDTO>("channels");
        _twitchAccountsCollection = mongoDatabase.GetCollection<TwitchAccountDTO>("twitch_accounts");
        _twitchUsersMessageTimeCollection = mongoDatabase.GetCollection<TwitchUserMessageTime>("twitch_users_message_time");
        _twitchUserSubscriptionCollection = mongoDatabase.GetCollection<TwitchUserSubscriptionDTO>("twitch_user_subscriptions");
        _twitchUserStatsCollection = mongoDatabase.GetCollection<TwitchUserStatDTO>("twitch_user_stats");

        _twitchWordUserStatCollections = new();
        _twitchWordStatCollections = new();

        cancellationToken = new CancellationTokenSource();

        Console.CancelKeyPress += Console_CancelKeyPress;

        _ = Task.Run(ArchiveLogsLoop, cancellationToken.Token);

        ircBot = new TwitchIrcBot("TwitchLogger", "");
        ircBot.OnMessage += Irc_OnMessage;
        await ircBot.Start();

        while (true)
        {
            //Get all channels/join/leave
            //

            HashSet<string> channelsToJoin = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> channelsToLeave = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> allChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await (await _channelsCollection.FindAsync(Builders<ChannelDTO>.Filter.Empty)).ForEachAsync(x =>
            {
                var channel = $"#{x.Login}";
                if (!ircBot.ChannelLists.Contains(channel))
                {
                    _twitchWordUserStatCollections[x.UserId] = mongoDatabase.GetCollection<TwitchWordUserStatDTO>($"twitch_word_user_stat_{x.UserId}");
                    _twitchWordStatCollections[x.UserId] = mongoDatabase.GetCollection<TwitchWordStatDTO>($"twitch_word_stat_{x.UserId}");

                    channelsToJoin.Add(channel);
                }

                allChannels.Add(channel);
            });

            foreach (var channel in ircBot.ChannelLists)
            {
                if (!allChannels.Contains(channel))
                    channelsToLeave.Add(channel);
            }

            foreach (var channel in channelsToLeave)
            {
                Console.WriteLine($"[BOT] Leave {channel}");
                await ircBot.LeaveChannel(channel);
                await Task.Delay(1200);
            }

            foreach (var channel in channelsToJoin)
            {
                Console.WriteLine($"[BOT] Join {channel}");
                await ircBot.JoinChannel(channel);
                await Task.Delay(1200);
            }

            try
            {
                await Task.Delay(1000 * 60 * 5, cancellationToken.Token);
            }
            catch
            {
                break;
            }
        }
    }
};