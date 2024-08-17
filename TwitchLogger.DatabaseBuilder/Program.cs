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
using System.Threading;
using System;
using TwitchLogger.ChatBot;
using System.Net.Http;
using System.Net.Http.Json;
using static MongoDB.Driver.WriteConcern;

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
    private static IMongoDatabase _mongoDatabase;
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

    static HashSet<string> GetWords(string message, out int initLen, HashSet<string> allWords)
    {
        HashSet<string> list = new(StringComparer.InvariantCultureIgnoreCase);
        var splited = message.Split(' ');

        foreach (var word in splited)
        {
            if (!string.IsNullOrEmpty(word) && IsLegalUnicode(word))
            {
                list.Add(word);
                allWords.Add(word);
            }
        }

        initLen = list.Count;
        return list;
    }

    static SemaphoreSlim[] locksUpdate = new SemaphoreSlim[10]
    {
       new SemaphoreSlim(1, 1),
       new SemaphoreSlim(1, 1),
       new SemaphoreSlim(1, 1),
       new SemaphoreSlim(1, 1),
       new SemaphoreSlim(1, 1),
       new SemaphoreSlim(1, 1),
       new SemaphoreSlim(1, 1),
       new SemaphoreSlim(1, 1),
       new SemaphoreSlim(1, 1),
       new SemaphoreSlim(1, 1)
    };

    private static object _lockwrite = new object();
    private static long totalProcessedLines = 0;

    static void ProcessLogFileForEmotes(string file)
    {
        long currentProcessedLines = 0;

        var isGz = file.EndsWith(".gz");

        using (FileStream fileStream = File.OpenRead(file))
        {
            Stream stream = fileStream;

            if (isGz)
                stream = new GZipStream(fileStream, CompressionMode.Decompress, true);

            using (StreamReader streamReader = new StreamReader(stream, System.Text.Encoding.UTF8))
            {
                Dictionary<string, EmoteInfo> usedEmotes = new Dictionary<string, EmoteInfo>();
                Dictionary<string, int> usedEmotesCount = new Dictionary<string, int>();

                string roomId = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(file)))));
                int year = int.Parse(Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file))));

                _channelsEmotesSet.TryGetValue(roomId, out var customChannelEmotes);

                while (true)
                {
                    var command = streamReader.ReadLine();
                    if (command == null) break;

                    currentProcessedLines++;

                    var commandArgs = command.Split(' ');
                    if (commandArgs[2] != "PRIVMSG")
                        continue;

                    string channel = commandArgs[3].Trim();
                    string message = commandArgs.Length > 4 ? string.Join(' ', commandArgs.Skip(4)).Substring(1) : "";
                    if (!string.IsNullOrEmpty(message) && message[0] == '\x01')
                    {
                        var msgStartIndex = message.IndexOf(' ');
                        if (msgStartIndex != -1)
                            message = message.Substring(msgStartIndex + 1, message.Length - msgStartIndex - 2);
                    }

                    HashSet<string> allWords = new HashSet<string>();
                    GetWords(message, out var initLen, allWords);

                    foreach (var word in allWords)
                    {
                        if (customChannelEmotes != null)
                        {
                            if (customChannelEmotes.TryGetValue(word, out var emoteData))
                            {
                                usedEmotes[word] = new EmoteInfo() { Type = emoteData.Type, Url = emoteData.Url };
                                usedEmotesCount.TryGetValue(word, out var count);
                                usedEmotesCount[word] = count + 1;
                            }
                        }
                    }
                }

                List<UpdateOneModel<TwitchEmoteStatDTO>> emoteStatUpdates = new List<UpdateOneModel<TwitchEmoteStatDTO>>(usedEmotes.Count * 2);
                var filterEmoteStat = Builders<TwitchEmoteStatDTO>.Filter;

                foreach (var emote in usedEmotes)
                {
                    var count = (ulong)usedEmotesCount[emote.Key];

                    emoteStatUpdates.Add(new UpdateOneModel<TwitchEmoteStatDTO>(filterEmoteStat.Eq(x => x.RoomId, roomId) & filterEmoteStat.Eq(x => x.Year, 0) & filterEmoteStat.Eq(x => x.Emote, emote.Key), Builders<TwitchEmoteStatDTO>.Update.Inc(x => x.Count, count).Set(x => x.EmoteType, emote.Value.Type).Set(x => x.Url, emote.Value.Url))
                    {
                        IsUpsert = true
                    });

                    emoteStatUpdates.Add(new UpdateOneModel<TwitchEmoteStatDTO>(filterEmoteStat.Eq(x => x.RoomId, roomId) & filterEmoteStat.Eq(x => x.Year, year) & filterEmoteStat.Eq(x => x.Emote, emote.Key), Builders<TwitchEmoteStatDTO>.Update.Inc(x => x.Count, count).Set(x => x.EmoteType, emote.Value.Type).Set(x => x.Url, emote.Value.Url))
                    {
                        IsUpsert = true
                    });
                }

                if (emoteStatUpdates.Count > 0)
                {
                    lock (_lockwrite)
                    {
                        _twitchEmoteStatCollection.BulkWrite(emoteStatUpdates);
                    }
                }

                if (isGz)
                    stream.Dispose();
            }
        }

        Interlocked.Add(ref totalProcessedLines, currentProcessedLines);
    }

    static async Task ProcessLogFile(string file)
    {
        var isGz = file.EndsWith(".gz");

        var filterTwitchAccount = Builders<TwitchAccountDTO>.Filter;
        var filterTwitchUserMessageTime = Builders<TwitchUserMessageTime>.Filter;
        var filterUserStat = Builders<TwitchUserStatDTO>.Filter;
        var filterWordUserStat = Builders<TwitchWordUserStatDTO>.Filter;
        var filterWordStat = Builders<TwitchWordStatDTO>.Filter;

        Dictionary<string, Tuple<long, UpdateOneModel<TwitchAccountDTO>>> twitchAccountsStatic = new Dictionary<string, Tuple<long, UpdateOneModel<TwitchAccountDTO>>>();
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

                            if (twitchAccountsStatic.TryGetValue(userId, out var twitchAccount))
                            {
                                if (unixCurrentDate > twitchAccount.Item1)
                                    twitchAccountsStatic[userId] = new Tuple<long, UpdateOneModel<TwitchAccountDTO>>(unixCurrentDate, new UpdateOneModel<TwitchAccountDTO>(filterTwitchAccount.Eq(x => x.UserId, userId), Builders<TwitchAccountDTO>.Update.Set(x => x.Login, userLogin).Set(x => x.DisplayName, userDisplayname).Set(x => x.RecordInsertTime, unixCurrentDate)) { IsUpsert = true });
                            }
                            else
                                twitchAccountsStatic[userId] = new Tuple<long, UpdateOneModel<TwitchAccountDTO>>(unixCurrentDate, new UpdateOneModel<TwitchAccountDTO>(filterTwitchAccount.Eq(x => x.UserId, userId), Builders<TwitchAccountDTO>.Update.Set(x => x.Login, userLogin).Set(x => x.DisplayName, userDisplayname).Set(x => x.RecordInsertTime, unixCurrentDate)) { IsUpsert = true });

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

                                HashSet<string> allWords = new HashSet<string>();
                                var wordsMessage = GetWords(message, out var initLen, allWords);

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

        await locksUpdate[0].WaitAsync();
        if (channelSubsUpdate.Count > 0)
            await _twitchUserSubscriptionCollection.InsertManyAsync(channelSubsUpdate);
        locksUpdate[0].Release();

        foreach (var wordStat in wordsUpdate)
        {
            var data = wordStat.Key.Split('?');
            List<UpdateOneModel<TwitchWordStatDTO>> wordStats = new();

            foreach (var word in wordStat.Value)
            {
                wordStats.Add(new UpdateOneModel<TwitchWordStatDTO>(filterWordStat.Eq(x => x.RoomId, data[0]) & filterWordStat.Eq(x => x.Word, word.Key) & filterWordStat.Eq(x => x.Year, int.Parse(data[1])), Builders<TwitchWordStatDTO>.Update.Inc(x => x.Count, word.Value)) { Collation = ignoreCaseCollation, IsUpsert = true });
            }

            await locksUpdate[1].WaitAsync();
            if (wordStats.Count > 0)
                await _twitchWordStatCollection.BulkWriteAsync(wordStats);
            locksUpdate[1].Release();
        }

        foreach (var userWordStat in userWordsUpdate)
        {
            List<UpdateOneModel<TwitchWordUserStatDTO>> userWordStats = new();
            foreach (var item in userWordStat.Value)
            {
                var data = item.Key.Split('?');
                foreach (var word in item.Value)
                {
                    userWordStats.Add(new UpdateOneModel<TwitchWordUserStatDTO>(filterWordUserStat.Eq(x => x.RoomId, userWordStat.Key) & filterWordUserStat.Eq(x => x.UserId, data[0]) & filterWordUserStat.Eq(x => x.Word, word.Key) & filterWordUserStat.Eq(x => x.Year, int.Parse(data[1])), Builders<TwitchWordUserStatDTO>.Update.Inc(x => x.Count, word.Value)) { Collation = ignoreCaseCollation, IsUpsert = true });
                }
            }
            await locksUpdate[2].WaitAsync();
            if (userWordStats.Count > 0)
                await _twitchWordUserStatCollection.BulkWriteAsync(userWordStats);
            locksUpdate[2].Release();
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
            await locksUpdate[3].WaitAsync();
            if (usersStatsUpdates.Count > 0)
                await _twitchUserStatsCollection.BulkWriteAsync(usersStatsUpdates);
            locksUpdate[3].Release();
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

        await locksUpdate[4].WaitAsync();

        if (messageTimes.Count > 0)
            await _twitchUsersMessageTimeCollection.BulkWriteAsync(messageTimes);

        locksUpdate[4].Release();

        List<UpdateOneModel<ChannelDTO>> channelsUpdateBulk = new();
        foreach (var channel in channelsUpdate)
        {
            channelsUpdateBulk.Add(new UpdateOneModel<ChannelDTO>(Builders<ChannelDTO>.Filter.Eq(x => x.UserId, channel.Key), Builders<ChannelDTO>.Update.Inc(x => x.MessageCount, channel.Value.Second)));
            channelsUpdateBulk.Add(new UpdateOneModel<ChannelDTO>(Builders<ChannelDTO>.Filter.Eq(x => x.UserId, channel.Key), Builders<ChannelDTO>.Update.Max(x => x.MessageLastDate, channel.Value.First)));
        }
        await locksUpdate[5].WaitAsync();

        if (channelsUpdateBulk.Count > 0)
            await _channelsCollection.BulkWriteAsync(channelsUpdateBulk);

        locksUpdate[5].Release();

        await locksUpdate[6].WaitAsync();

        if (twitchAccounts.Values.Count > 0)
            await _twitchAccountsCollection.BulkWriteAsync(twitchAccounts.Values);

        if (twitchAccountsStatic.Values.Count > 0)
            await _twitchAccountsStaticCollection.BulkWriteAsync(twitchAccountsStatic.Values.Select(x => x.Item2));

        locksUpdate[6].Release();
    }

    static async Task Main(string[] args)
    {
        JObject settings = JObject.Parse(await File.ReadAllTextAsync("appsettings.json"));
        var connectionString = settings["mongo"]["connectionString"].ToString();
        var databaseName = settings["mongo"]["databaseName"].ToString();

        Console.WriteLine("Drop database (y/n)?");
        var dropDatabase = Console.ReadLine().Trim() == "y";

        Console.WriteLine("Drop indexes (y/n)?");
        var dropIndexes = Console.ReadLine().Trim() == "y";

        var skipExistingChannels = false;
        if (!dropDatabase)
        {
            Console.WriteLine("Skip existing channels (y/n)?");
            skipExistingChannels = Console.ReadLine().Trim() == "y";
        }

        Console.WriteLine("Dataset date range format {start-end} example -2023.09");
        var dateRange = Console.ReadLine();
        var startDate = DateTimeOffset.FromUnixTimeSeconds(0);
        var endDate = DateTimeOffset.UtcNow.AddYears(1);

        if (dateRange.IndexOf('-') != -1)
        {
            var splitted = dateRange.Split('-');
            if (!string.IsNullOrEmpty(splitted[0]))
                startDate = DateTimeOffset.ParseExact(splitted[0], "yyyy.MM", CultureInfo.InvariantCulture.DateTimeFormat);

            if (!string.IsNullOrEmpty(splitted[1]))
                endDate = DateTimeOffset.ParseExact(splitted[1], "yyyy.MM", CultureInfo.InvariantCulture.DateTimeFormat);
        }

        var _client = new MongoClient(connectionString);
        if (dropDatabase)
        {
            Console.WriteLine("Cleanup...");
            await _client.DropDatabaseAsync(databaseName);
        }

        _mongoDatabase = _client.GetDatabase(databaseName);

        _channelsCollection = _mongoDatabase.GetCollection<ChannelDTO>("channels");
        _twitchAccountsCollection = _mongoDatabase.GetCollection<TwitchAccountDTO>("twitch_accounts");
        _twitchAccountsStaticCollection = _mongoDatabase.GetCollection<TwitchAccountDTO>("twitch_accounts_static");
        _twitchUsersMessageTimeCollection = _mongoDatabase.GetCollection<TwitchUserMessageTime>("twitch_users_message_time");
        _twitchUserSubscriptionCollection = _mongoDatabase.GetCollection<TwitchUserSubscriptionDTO>("twitch_user_subscriptions");
        _twitchUserStatsCollection = _mongoDatabase.GetCollection<TwitchUserStatDTO>("twitch_user_stats");
        _twitchWordUserStatCollection = _mongoDatabase.GetCollection<TwitchWordUserStatDTO>("twitch_word_user_stats");
        _twitchWordStatCollection = _mongoDatabase.GetCollection<TwitchWordStatDTO>("twitch_word_stats");
        _twitchEmoteStatCollection = _mongoDatabase.GetCollection<TwitchEmoteStatDTO>("twitch_emote_stats");

        Console.WriteLine("Starting...");

        //Create indexes
        //
        {
            if (dropIndexes)
            {
                await _channelsCollection.Indexes.DropAllAsync();
                await _twitchAccountsCollection.Indexes.DropAllAsync();
                await _twitchUsersMessageTimeCollection.Indexes.DropAllAsync();
                await _twitchUserSubscriptionCollection.Indexes.DropAllAsync();
                await _twitchUserStatsCollection.Indexes.DropAllAsync();
                await _twitchWordUserStatCollection.Indexes.DropAllAsync();
                await _twitchWordStatCollection.Indexes.DropAllAsync();
            }

            await _channelsCollection.Indexes.CreateOneAsync(new CreateIndexModel<ChannelDTO>(Builders<ChannelDTO>.IndexKeys.Ascending(x => x.UserId), new CreateIndexOptions() { Unique = true }));

            await _twitchAccountsStaticCollection.Indexes.CreateOneAsync(new CreateIndexModel<TwitchAccountDTO>(Builders<TwitchAccountDTO>.IndexKeys.Ascending(x => x.UserId), new CreateIndexOptions() { Unique = true }));

            await _twitchAccountsCollection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<TwitchAccountDTO>(Builders<TwitchAccountDTO>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.Login), new CreateIndexOptions() { Unique = true }),
                new CreateIndexModel<TwitchAccountDTO>(Builders<TwitchAccountDTO>.IndexKeys.Ascending(x => x.Login).Descending(x => x.RecordInsertTime), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) })
            });

            await _twitchUsersMessageTimeCollection.Indexes.CreateOneAsync(new CreateIndexModel<TwitchUserMessageTime>(Builders<TwitchUserMessageTime>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.UserId), new CreateIndexOptions() { Unique = true }));

            await _twitchUserSubscriptionCollection.Indexes.CreateOneAsync(new CreateIndexModel<TwitchUserSubscriptionDTO>(Builders<TwitchUserSubscriptionDTO>.IndexKeys.Ascending(x => x.RoomId).Descending(x => x.Timestamp)));

            await _twitchUserStatsCollection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<TwitchUserStatDTO>(Builders<TwitchUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Ascending(x => x.UserId), new CreateIndexOptions() { Unique = true }),
                new CreateIndexModel<TwitchUserStatDTO>(Builders<TwitchUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Descending(x => x.Messages)),
            });

            await _twitchWordUserStatCollection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Ascending(x => x.UserId).Ascending(x => x.Word), new CreateIndexOptions() { Unique = true, Collation = new Collation("en", strength: CollationStrength.Secondary) }),
                new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Ascending(x => x.UserId).Descending(x => x.Count)),
                new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Ascending(x => x.Word).Descending(x => x.Count), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) })
            });

            await _twitchWordStatCollection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<TwitchWordStatDTO>(Builders<TwitchWordStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Ascending(x => x.Word), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary), Unique = true }),
                new CreateIndexModel<TwitchWordStatDTO>(Builders<TwitchWordStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Descending(x => x.Count))
            });

            await _twitchEmoteStatCollection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<TwitchEmoteStatDTO>(Builders<TwitchEmoteStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Ascending(x => x.Emote), new CreateIndexOptions() {Unique = true })
            });
        }

        Console.WriteLine("Indexes done...");

        var logsPath = settings["Logs"]["DataLogDirectory"].ToString();

        if (!Directory.Exists(logsPath))
            return;

        HttpClient httpClient = new HttpClient();

        var globalEmotes7Yv = await httpClient.GetFromJsonAsync<TV7UserEmotesEmoteSet>("https://7tv.io/v3/emote-sets/global");
        var betterTTVEmotes = await httpClient.GetFromJsonAsync<BetterTTVEmote[]>("https://api.betterttv.net/3/cached/emotes/global");

        var directories = Directory.GetDirectories(logsPath);
        for (var i = 0; i < directories.Length; i++)
        {
            Console.WriteLine($"Starting channel process {i + 1}/{directories.Length}...");

            var channel = directories[i];
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

            var channelExisted = false;
            if (await (await _channelsCollection.FindAsync(x => x.UserId == channelData.Id)).FirstOrDefaultAsync() == null)
                await _channelsCollection.InsertOneAsync(new ChannelDTO() { UserId = channelData.Id, Login = channelData.Login, DisplayName = channelData.DisplayName, LogoUrl = channelData.ProfileImageURL, MessageCount = 0, MessageLastDate = 0, StartTrackingDate = result.Item2 });
            else
                channelExisted = true;

            if (!channelExisted || !skipExistingChannels)
            {
                var channelEmotes = new Dictionary<string, EmoteData>();

                try
                {
                    var channel7Tv = await httpClient.GetFromJsonAsync<TV7UserEmotes>("https://7tv.io/v3/users/twitch/" + channelData.Id);
                    if (channel7Tv.EmoteSet != null)
                    {
                        Parse7tvEmoteSet(globalEmotes7Yv, channelEmotes);
                        Parse7tvEmoteSet(channel7Tv.EmoteSet, channelEmotes);
                    }
                }
                catch { }

                try
                {
                    var emotes = await TwitchGraphQL.GetChannelEmotes(channelData.Id);
                    ParseTwitchEmoteSet(TwitchGraphQL.TwitchGlobalEmotes, channelEmotes);
                    ParseTwitchEmoteSet(emotes, channelEmotes);
                }
                catch { }

                try
                {
                    var channelBttv = await httpClient.GetFromJsonAsync<BetterTTVChannel>("https://api.betterttv.net/3/cached/users/twitch/" + channelData.Id);
                    if (channelBttv.SharedEmotes != null
                        || channelBttv.ChannelEmotes != null)
                    {
                        ParseBetterTTVEmoteSet(betterTTVEmotes, channelEmotes);
                        ParseBetterTTVEmoteSet(channelBttv.SharedEmotes, channelEmotes);
                        ParseBetterTTVEmoteSet(channelBttv.ChannelEmotes, channelEmotes);
                    }
                }
                catch { }

                _channelsEmotesSet[channelData.Id] = channelEmotes;

                var currentFiles = 0;
                var files = logsFiles.Where(x => x.Item1 >= startDate && x.Item1 < endDate).Select(x => x.Item2).ToList();

                totalProcessedLines = 0;

                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = -1 }, item =>
                {
                    ProcessLogFileForEmotes(item);
                    Console.WriteLine($"Done item process {++currentFiles}/{files.Count}");
                });

                Console.WriteLine($"{channelData.Login} processed {totalProcessedLines}");
            }

            Console.WriteLine($"Done channel process {i + 1}/{directories.Length}");
        }

        Console.WriteLine("Imported!");
        Console.ReadLine();
    }
}