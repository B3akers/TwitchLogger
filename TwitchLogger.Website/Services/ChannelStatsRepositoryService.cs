using MongoDB.Driver;
using TwitchLogger.DTO;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Services
{
    public class ChannelStatsRepositoryService : IChannelStatsRepository
    {
        private readonly DatabaseService _databaseService;

        private readonly Collation _ignoreCaseCollation;

        public ChannelStatsRepositoryService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _ignoreCaseCollation = new Collation("en", strength: CollationStrength.Secondary);
        }

        public async Task<IEnumerable<TwitchWordUserStatDTO>> GetTopUserWords(string channelId, string user, int year, int limit)
        {
            var wordUserStats = _databaseService.GetTwitchWordUserStatCollection();
            return await (await wordUserStats.FindAsync(x => x.RoomId == channelId && x.UserId == user && x.Year == year, new FindOptions<TwitchWordUserStatDTO>()
            {
                Collation = _ignoreCaseCollation,
                Limit = limit,
                Sort = Builders<TwitchWordUserStatDTO>.Sort.Descending(x => x.Count)
            })).ToListAsync();
        }

        public async Task<IEnumerable<TwitchWordUserStatDTO>> GetTopUsers(string channelId, string word, int year, int limit)
        {
            var wordUserStats = _databaseService.GetTwitchWordUserStatCollection();
            return await (await wordUserStats.FindAsync(x => x.RoomId == channelId && x.Word == word && x.Year == year, new FindOptions<TwitchWordUserStatDTO>()
            {
                Collation = _ignoreCaseCollation,
                Limit = limit,
                Sort = Builders<TwitchWordUserStatDTO>.Sort.Descending(x => x.Count)
            })).ToListAsync();
        }

        public async Task<TwitchUserMessageTime> GetUserMessageTime(string channelId, string user)
        {
            var usersMessageTime = _databaseService.GetTwitchUsersMessageTimeCollection();

            return await (await usersMessageTime.FindAsync(x => x.UserId == user && x.RoomId == channelId)).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<TwitchWordStatDTO>> GetTopWords(string channelId, int year, int limit)
        {
            var wordStats = _databaseService.GetTwitchWordStatCollection();
            return await (await wordStats.FindAsync(x => x.RoomId == channelId && x.Year == year, new FindOptions<TwitchWordStatDTO>()
            {
                Limit = limit,
                Sort = Builders<TwitchWordStatDTO>.Sort.Descending(x => x.Count)
            })).ToListAsync();
        }

        public async Task<IEnumerable<TwitchUserStatDTO>> GetTopChatters(string channelId, int year, int limit)
        {
            var userStats = _databaseService.GetTwitchUserStatsCollection();
            return await (await userStats.FindAsync(x => x.RoomId == channelId && x.Year == year, new FindOptions<TwitchUserStatDTO>()
            {
                Limit = limit,
                Sort = Builders<TwitchUserStatDTO>.Sort.Descending(x => x.Messages)
            })).ToListAsync();
        }

        public async Task<List<Tuple<string, int>>> GetUniqueSubscriptions(string channelId, long from, long to)
        {
            var twitchSubscriptions = _databaseService.GetTwitchUserSubscriptionsCollection();
            var result = new List<Tuple<string, int>>();
            Dictionary<string, HashSet<string>> group = new();

            await (await twitchSubscriptions.FindAsync(x => x.RoomId == channelId && x.Timestamp >= from && x.Timestamp <= to)).ForEachAsync(x =>
            {
                if (!group.TryGetValue(x.SubPlan, out var result))
                {
                    result = new();
                    group.Add(x.SubPlan, result);
                }

                result.Add(x.RecipientUserId);
            });

            foreach (var it in group)
                result.Add(new Tuple<string, int>(it.Key, it.Value.Count));

            result.Sort((x, y) => y.Item2 - x.Item2);

            return result;
        }
    }
}
