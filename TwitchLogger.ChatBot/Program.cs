using MongoDB.Driver;
using MongoDB.Driver.Core.Bindings;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Pipes;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Channels;
using TwitchLogger.ChatBot;
using TwitchLogger.ChatBot.TwitchIrcClient;
using TwitchLogger.DTO;
using TwitchLogger.SimpleGraphQL;

public class EmoteData
{
    public string Url { get; set; }
    public string Type { get; set; }
}

public struct EmoteInfo
{
    public string Type;
    public string Url;
}

class Program
{
    private static CancellationTokenSource cancellationToken;
    private static TwitchIrcBot ircBot;
    private static MongoClient _client;

    // private static NamedPipeClientStream _clientPipeStream;
    // private static SemaphoreSlim _clientPipeCreationLock = new SemaphoreSlim(1, 1);

    private static IMongoCollection<ChannelDTO> _channelsCollection;
    private static IMongoCollection<TwitchAccountDTO> _twitchAccountsCollection;
    private static IMongoCollection<TwitchAccountDTO> _twitchAccountsStaticCollection;
    private static IMongoCollection<TwitchUserMessageTime> _twitchUsersMessageTimeCollection;
    private static IMongoCollection<TwitchUserSubscriptionDTO> _twitchUserSubscriptionCollection;
    private static IMongoCollection<TwitchUserStatDTO> _twitchUserStatsCollection;
    private static IMongoCollection<TwitchWordUserStatDTO> _twitchWordUserStatCollection;
    private static IMongoCollection<TwitchWordStatDTO> _twitchWordStatCollection;
    private static IMongoCollection<TwitchEmoteStatDTO> _twitchEmoteStatCollection;

    private static Collation ignoreCaseCollation = new Collation("en", strength: CollationStrength.Secondary);
    private static Dictionary<string, Dictionary<string, EmoteData>> _channelsEmotesSet = new Dictionary<string, Dictionary<string, EmoteData>>();

    private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
    {
        if (ircBot != null)
            ircBot.Disconnect();
        cancellationToken.Cancel();

        Console.WriteLine("Exiting...");

        e.Cancel = true;
    }
    private static void ParseTwitchEmoteSet(IEnumerable<TwitchEmote> emoteSet, Dictionary<string, EmoteData> emotesDict)
    {
        if (emoteSet == null)
            return;

        foreach (var emote in emoteSet)
        {
            var emoteUrl = $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.Id}/default/dark/1.0";

            emotesDict[emote.Token] = new EmoteData() { Url = emoteUrl, Type = "twitch" };
        }
    }

    private static void ParseBetterTTVEmoteSet(BetterTTVEmote[] emoteSet, Dictionary<string, EmoteData> emotesDict)
    {
        if (emoteSet == null)
            return;

        for (var i = 0; i < emoteSet.Length; i++)
        {
            var emote = emoteSet[i];
            var emoteUrl = $"https://cdn.betterttv.net/emote/{emote.Id}/1x";

            emotesDict[emote.Code] = new EmoteData() { Url = emoteUrl, Type = "bttv" };
        }
    }

    private static void Parse7tvEmoteSet(TV7UserEmotesEmoteSet emoteSet, Dictionary<string, EmoteData> emotesDict)
    {
        if (emoteSet == null)
            return;

        var emotes = emoteSet?.Emotes;
        if (emotes == null)
            return;

        for (var i = 0; i < emotes.Count; i++)
        {
            var emote = emotes[i];
            var emoteUrl = emote.Data.Host.Url + "/1x.avif";

            emotesDict[emote.Name] = new EmoteData() { Url = emoteUrl, Type = "7tv" };
        }
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

    private static HashSet<string> GetWords(string message, out int initLen, HashSet<string> allWords)
    {
        HashSet<string> list = new(StringComparer.InvariantCultureIgnoreCase);
        var splited = message.Split(' ');

        initLen = splited.Length;

        foreach (var word in splited)
        {
            if (!string.IsNullOrEmpty(word) && IsLegalUnicode(word))
            {
                list.Add(word);
                allWords.Add(word);
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

                await _twitchAccountsStaticCollection.UpdateOneAsync(x => x.UserId == userId, Builders<TwitchAccountDTO>.Update.Set(x => x.Login, userLogin).Set(x => x.DisplayName, userDisplayname).Set(x => x.RecordInsertTime, unixCurrentDate), new UpdateOptions() { IsUpsert = true });
                await _twitchAccountsCollection.UpdateOneAsync(x => x.UserId == userId && x.Login == userLogin, Builders<TwitchAccountDTO>.Update.Set(x => x.DisplayName, userDisplayname).SetOnInsert(x => x.RecordInsertTime, unixCurrentDate), new UpdateOptions() { IsUpsert = true });
            }

            //var messageObject = new JObject();

            if (args.MessageType == TwitchChatMessageType.PRIVMSG)
            {
                var roomId = args.SenderInfo["room-id"];
                var userId = args.SenderInfo["user-id"];
                var userDisplayname = args.SenderInfo["display-name"];

                Dictionary<string, EmoteInfo> usedEmotes = new Dictionary<string, EmoteInfo>();

                await _twitchUsersMessageTimeCollection.UpdateOneAsync(x => x.UserId == userId && x.RoomId == roomId, Builders<TwitchUserMessageTime>.Update.AddToSet(x => x.MessageTimes, currentDate.ToString("yyyy-MM")), new UpdateOptions() { IsUpsert = true });
                await _channelsCollection.UpdateOneAsync(x => x.UserId == roomId, Builders<ChannelDTO>.Update.Set(x => x.MessageLastDate, unixCurrentDate).Inc(x => x.MessageCount, 1ul));

                List<UpdateOneModel<TwitchWordUserStatDTO>> wordUserStatUpdates = new List<UpdateOneModel<TwitchWordUserStatDTO>>();
                List<UpdateOneModel<TwitchWordStatDTO>> wordStatUpdates = new List<UpdateOneModel<TwitchWordStatDTO>>();

                HashSet<string> allWords = new HashSet<string>();
                var wordsMessage = GetWords(args.Message, out var initLen, allWords);
                var filterWordUserStat = Builders<TwitchWordUserStatDTO>.Filter;
                var filterWordStat = Builders<TwitchWordStatDTO>.Filter;
                var filterUserStat = Builders<TwitchUserStatDTO>.Filter;
                var year = int.Parse(currentDate.ToString("yyyy"));

                //messageObject["type"] = "PRIVMSG";
                //messageObject["roomId"] = roomId;
                //messageObject["user"] = userDisplayname;
                //messageObject["timestamp"] = unixCurrentDate;
                //messageObject["message"] = args.Message;
                //messageObject["words"] = JArray.FromObject(wordsMessage);

                _channelsEmotesSet.TryGetValue(roomId, out var customChannelEmotes);

                if (customChannelEmotes != null)
                {
                    foreach (var word in allWords)
                    {
                        if (customChannelEmotes.TryGetValue(word, out var emoteData))
                        {
                            usedEmotes[word] = new EmoteInfo() { Type = emoteData.Type, Url = emoteData.Url };
                        }
                    }
                }

                foreach (var word in wordsMessage)
                {
                    wordUserStatUpdates.Add(new UpdateOneModel<TwitchWordUserStatDTO>(filterWordUserStat.Eq(x => x.RoomId, roomId) & filterWordUserStat.Eq(x => x.UserId, userId) & filterWordUserStat.Eq(x => x.Word, word) & filterWordUserStat.Eq(x => x.Year, year), Builders<TwitchWordUserStatDTO>.Update.Inc(x => x.Count, 1ul))
                    {
                        Collation = ignoreCaseCollation,
                        IsUpsert = true
                    });

                    wordUserStatUpdates.Add(new UpdateOneModel<TwitchWordUserStatDTO>(filterWordUserStat.Eq(x => x.RoomId, roomId) & filterWordUserStat.Eq(x => x.UserId, userId) & filterWordUserStat.Eq(x => x.Word, word) & filterWordUserStat.Eq(x => x.Year, 0), Builders<TwitchWordUserStatDTO>.Update.Inc(x => x.Count, 1ul))
                    {
                        Collation = ignoreCaseCollation,
                        IsUpsert = true
                    });

                    wordStatUpdates.Add(new UpdateOneModel<TwitchWordStatDTO>(filterWordStat.Eq(x => x.RoomId, roomId) & filterWordStat.Eq(x => x.Word, word) & filterWordStat.Eq(x => x.Year, year), Builders<TwitchWordStatDTO>.Update.Inc(x => x.Count, 1ul))
                    {
                        Collation = ignoreCaseCollation,
                        IsUpsert = true
                    });

                    wordStatUpdates.Add(new UpdateOneModel<TwitchWordStatDTO>(filterWordStat.Eq(x => x.RoomId, roomId) & filterWordStat.Eq(x => x.Word, word) & filterWordStat.Eq(x => x.Year, 0), Builders<TwitchWordStatDTO>.Update.Inc(x => x.Count, 1ul))
                    {
                        Collation = ignoreCaseCollation,
                        IsUpsert = true
                    });
                }

                List<UpdateOneModel<TwitchEmoteStatDTO>> emoteStatUpdates = new List<UpdateOneModel<TwitchEmoteStatDTO>>(usedEmotes.Count * 2);
                var filterEmoteStat = Builders<TwitchEmoteStatDTO>.Filter;

                foreach (var emote in usedEmotes)
                {
                    emoteStatUpdates.Add(new UpdateOneModel<TwitchEmoteStatDTO>(filterEmoteStat.Eq(x => x.RoomId, roomId) & filterEmoteStat.Eq(x => x.Year, 0) & filterEmoteStat.Eq(x => x.Emote, emote.Key), Builders<TwitchEmoteStatDTO>.Update.Inc(x => x.Count, 1ul).Set(x => x.EmoteType, emote.Value.Type).Set(x => x.Url, emote.Value.Url))
                    {
                        IsUpsert = true
                    });

                    emoteStatUpdates.Add(new UpdateOneModel<TwitchEmoteStatDTO>(filterEmoteStat.Eq(x => x.RoomId, roomId) & filterEmoteStat.Eq(x => x.Year, year) & filterEmoteStat.Eq(x => x.Emote, emote.Key), Builders<TwitchEmoteStatDTO>.Update.Inc(x => x.Count, 1ul).Set(x => x.EmoteType, emote.Value.Type).Set(x => x.Url, emote.Value.Url))
                    {
                        IsUpsert = true
                    });
                }

                if (emoteStatUpdates.Count > 0)
                    await _twitchEmoteStatCollection.BulkWriteAsync(emoteStatUpdates);

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
                    await _twitchWordUserStatCollection.BulkWriteAsync(wordUserStatUpdates);

                if (wordStatUpdates.Count > 0)
                    await _twitchWordStatCollection.BulkWriteAsync(wordStatUpdates);
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

            //if (messageObject.HasValues)
            //{
            //    if (_clientPipeStream == null)
            //    {
            //        await _clientPipeCreationLock.WaitAsync();
            //        if (_clientPipeStream == null)
            //        {
            //            try
            //            {
            //                _clientPipeStream = new NamedPipeClientStream("TwitchLogger.NamedPipe");
            //                await _clientPipeStream.ConnectAsync(1);
            //            }
            //            catch
            //            {
            //                await _clientPipeStream.DisposeAsync();
            //                _clientPipeStream = null;
            //            }
            //        }
            //        _clientPipeCreationLock.Release();
            //    }
            //
            //    if (_clientPipeStream != null)
            //    {
            //        try
            //        {
            //            await _clientPipeStream.WriteAsync(Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(messageObject.ToString(Newtonsoft.Json.Formatting.None))) + "\r\n"));
            //        }
            //        catch
            //        {
            //            await _clientPipeCreationLock.WaitAsync();
            //
            //            if (_clientPipeStream != null)
            //            {
            //                await _clientPipeStream.DisposeAsync();
            //                _clientPipeStream = null;
            //            }
            //
            //            _clientPipeCreationLock.Release();
            //        }
            //    }
            //}
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
        JObject settings = JObject.Parse(await File.ReadAllTextAsync("appsettings.json"));
        var connectionString = settings["mongo"]["connectionString"].ToString();
        var databaseName = settings["mongo"]["databaseName"].ToString();

        TwitchHelper.DataLogDirectory = settings["Logs"]["DataLogDirectory"].ToString();

        _client = new MongoClient(connectionString);
        var mongoDatabase = _client.GetDatabase(databaseName);

        _channelsCollection = mongoDatabase.GetCollection<ChannelDTO>("channels");
        _twitchAccountsCollection = mongoDatabase.GetCollection<TwitchAccountDTO>("twitch_accounts");
        _twitchAccountsStaticCollection = mongoDatabase.GetCollection<TwitchAccountDTO>("twitch_accounts_static");
        _twitchUsersMessageTimeCollection = mongoDatabase.GetCollection<TwitchUserMessageTime>("twitch_users_message_time");
        _twitchUserSubscriptionCollection = mongoDatabase.GetCollection<TwitchUserSubscriptionDTO>("twitch_user_subscriptions");
        _twitchUserStatsCollection = mongoDatabase.GetCollection<TwitchUserStatDTO>("twitch_user_stats");
        _twitchWordUserStatCollection = mongoDatabase.GetCollection<TwitchWordUserStatDTO>("twitch_word_user_stats");
        _twitchWordStatCollection = mongoDatabase.GetCollection<TwitchWordStatDTO>("twitch_word_stats");
        _twitchEmoteStatCollection = mongoDatabase.GetCollection<TwitchEmoteStatDTO>("twitch_emote_stats");

        cancellationToken = new CancellationTokenSource();

        Console.CancelKeyPress += Console_CancelKeyPress;

        _ = Task.Run(ArchiveLogsLoop, cancellationToken.Token);

        ircBot = new TwitchIrcBot("TwitchLogger", "");
        ircBot.OnMessage += Irc_OnMessage;
        await ircBot.Start();

        HttpClient httpClient = new HttpClient();

        TV7UserEmotesEmoteSet globalEmotes7Yv = null;
        BetterTTVEmote[] betterTTVEmotes = null;

        while (true)
        {
            //Get all channels/join/leave
            //

            HashSet<string> channelsToJoin = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> channelsToLeave = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> allChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> allChannelsIds = new HashSet<string>();

            await (await _channelsCollection.FindAsync(Builders<ChannelDTO>.Filter.Empty)).ForEachAsync(x =>
            {
                var channel = $"#{x.Login}";
                if (!ircBot.ChannelLists.ContainsKey(channel))
                {
                    bool isBanned = ircBot.BannedList.TryGetValue(channel, out var lastBanResponse);
                    if (!isBanned || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > (lastBanResponse + (3600 * 6)))
                        channelsToJoin.Add(channel);
                }

                allChannelsIds.Add(x.UserId);
                allChannels.Add(channel);
            });

            try
            {
                globalEmotes7Yv = await httpClient.GetFromJsonAsync<TV7UserEmotesEmoteSet>("https://7tv.io/v3/emote-sets/global");
            }
            catch { }

            try
            {
                betterTTVEmotes = await httpClient.GetFromJsonAsync<BetterTTVEmote[]>("https://api.betterttv.net/3/cached/emotes/global");
            }
            catch { }

            Dictionary<string, Dictionary<string, EmoteData>> channelsEmotesSet = new Dictionary<string, Dictionary<string, EmoteData>>();

            foreach (var channel in allChannelsIds)
            {
                var channelEmotes = new Dictionary<string, EmoteData>();

                try
                {
                    var channel7Tv = await httpClient.GetFromJsonAsync<TV7UserEmotes>("https://7tv.io/v3/users/twitch/" + channel);
                    if (channel7Tv.EmoteSet != null)
                    {
                        Parse7tvEmoteSet(globalEmotes7Yv, channelEmotes);
                        Parse7tvEmoteSet(channel7Tv.EmoteSet, channelEmotes);
                    }
                }
                catch { }

                try
                {
                    var emotes = await TwitchGraphQL.GetChannelEmotes(channel);
                    ParseTwitchEmoteSet(TwitchGraphQL.TwitchGlobalEmotes, channelEmotes);
                    ParseTwitchEmoteSet(emotes, channelEmotes);
                }
                catch { }

                try
                {
                    var channelBttv = await httpClient.GetFromJsonAsync<BetterTTVChannel>("https://api.betterttv.net/3/cached/users/twitch/" + channel);
                    if (channelBttv.SharedEmotes != null
                        || channelBttv.ChannelEmotes != null)
                    {
                        ParseBetterTTVEmoteSet(betterTTVEmotes, channelEmotes);
                        ParseBetterTTVEmoteSet(channelBttv.SharedEmotes, channelEmotes);
                        ParseBetterTTVEmoteSet(channelBttv.ChannelEmotes, channelEmotes);
                    }
                }
                catch { }

                if (channelEmotes.Count == 0)
                    continue;

                channelsEmotesSet[channel] = channelEmotes;
            }

            if (channelsEmotesSet.Count > 0)
            {
                _channelsEmotesSet = channelsEmotesSet;
            }

            foreach (var channel in ircBot.ChannelLists)
            {
                if (!allChannels.Contains(channel.Key))
                    channelsToLeave.Add(channel.Key);
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
                await Task.Delay(1000 * 60 * 30, cancellationToken.Token);
            }
            catch
            {
                break;
            }
        }
    }
};