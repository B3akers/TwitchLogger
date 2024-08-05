using MongoDB.Bson;
using MongoDB.Driver;
using TwitchLogger.DTO;
using TwitchLogger.Website.Interfaces;
using TwitchLogger.Website.Models;

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

        public async Task<List<TwitchWordUserStatDTO>> GetTopUserWords(string channelId, string user, int year, int limit)
        {
            var wordUserStats = _databaseService.GetTwitchWordUserStatCollection();
            return await (await wordUserStats.FindAsync(x => x.RoomId == channelId && x.UserId == user && x.Year == year, new FindOptions<TwitchWordUserStatDTO>()
            {
                Collation = _ignoreCaseCollation,
                Limit = limit,
                Sort = Builders<TwitchWordUserStatDTO>.Sort.Descending(x => x.Count)
            })).ToListAsync();
        }

        public async Task<List<TwitchWordUserStatDTO>> GetTopUsers(string channelId, string word, int year, int limit)
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

        public async Task<List<TwitchWordStatDTO>> GetTopWords(string channelId, int year, int limit)
        {
            var wordStats = _databaseService.GetTwitchWordStatCollection();
            return await (await wordStats.FindAsync(x => x.RoomId == channelId && x.Year == year, new FindOptions<TwitchWordStatDTO>()
            {
                Limit = limit,
                Sort = Builders<TwitchWordStatDTO>.Sort.Descending(x => x.Count)
            })).ToListAsync();
        }

        public async Task<List<TwitchWordStatDTO>> GetTopEmotes(string channelId, int year, string[] emotes)
        {
            var matchBuilder = Builders<TwitchWordStatDTO>.Filter;

            var wordStats = _databaseService.GetTwitchWordStatCollection();
            var pipeline = new EmptyPipelineDefinition<TwitchWordStatDTO>()
                .Match(matchBuilder.Eq(x => x.RoomId, channelId) & matchBuilder.Eq(x => x.Year, year) & matchBuilder.In(x => x.Word, emotes))
                .Sort(Builders<TwitchWordStatDTO>.Sort.Descending(x => x.Count));

            return await (await wordStats.AggregateAsync(pipeline, new AggregateOptions()
            {
                Collation = _ignoreCaseCollation
            })).ToListAsync();
        }

        public async Task<List<TwitchUserStatInfo>> GetTopChatters(string channelId, int year, int limit)
        {
            var userStats = _databaseService.GetTwitchUserStatsCollection();

            var project = Builders<TwitchUserStatDTO>.Projection.Expression(item => new TwitchUserStatInfo() { Stat = item });

            var pipeline = new EmptyPipelineDefinition<TwitchUserStatDTO>()
                .Match(x => x.RoomId == channelId && x.Year == year)
                .Sort(Builders<TwitchUserStatDTO>.Sort.Descending(x => x.Messages))
                .Limit(limit)
                .Project(project)
                .Lookup(_databaseService.GetTwitchAccountsStaticCollection(), x => x.Stat.UserId, x => x.UserId, (TwitchUserStatInfo p) => p.User);

            return await (await userStats.AggregateAsync(pipeline)).ToListAsync();
        }

        public async Task<List<UserTopSubscription>> GetTopSubscriptions(string channelId)
        {
            var twitchSubscriptions = _databaseService.GetTwitchUserSubscriptionsCollection();

            var pipeline = new EmptyPipelineDefinition<TwitchUserSubscriptionDTO>()
                .Match(Builders<TwitchUserSubscriptionDTO>.Filter.Eq(x => x.RoomId, channelId))
                .Group(x => x.RecipientUserId, g => new UserTopSubscription()
                {
                    _id = g.Key,
                    Months = g.Max(x => x.CumulativeMonths)
                })
                .Sort(Builders<UserTopSubscription>.Sort.Descending(x => x.Months))
                .Limit(10)
                .Lookup(_databaseService.GetTwitchAccountsStaticCollection(), x => x._id, x => x.UserId, (UserTopSubscription p) => p.User);

            return await (await twitchSubscriptions.AggregateAsync(pipeline)).ToListAsync();
        }

        public async Task<List<SubscriptionPlanInfo>> GetUniqueSubscriptions(string channelId, long from, long to)
        {
            var twitchSubscriptions = _databaseService.GetTwitchUserSubscriptionsCollection();
            var pipeline = new EmptyPipelineDefinition<TwitchUserSubscriptionDTO>()
                .Match(x => x.RoomId == channelId && x.Timestamp >= from && x.Timestamp <= to)
                .Group(x => x.SubPlan, g => new SubscriptionPlanInfo()
                {
                    _id = g.Key,
                    Count = g.Count()
                })
                .Sort(Builders<SubscriptionPlanInfo>.Sort.Descending(x => x.Count));

            return await (await twitchSubscriptions.AggregateAsync(pipeline)).ToListAsync();
        }

        public async Task<TwitchUserStatDTO> GetUserStats(string channelId, string user, int year = 0)
        {
            var userStats = _databaseService.GetTwitchUserStatsCollection();
            return await (await userStats.FindAsync(x => x.RoomId == channelId && x.Year == year && x.UserId == user)).FirstOrDefaultAsync();
        }

        public async Task<ulong> GetWordCount(string channelId, string word, string user, int year = 0)
        {
            if (string.IsNullOrEmpty(user))
            {
                var wordStats = _databaseService.GetTwitchWordStatCollection();

                var result = await (await wordStats.FindAsync(x => x.RoomId == channelId && x.Year == year && x.Word == word, new FindOptions<TwitchWordStatDTO>()
                {
                    Collation = _ignoreCaseCollation
                })).FirstOrDefaultAsync();

                return result == null ? 0 : result.Count;
            }
            else
            {
                var wordUserStats = _databaseService.GetTwitchWordUserStatCollection();

                var result = await (await wordUserStats.FindAsync(x => x.RoomId == channelId && x.Year == year && x.UserId == user && x.Word == word, new FindOptions<TwitchWordUserStatDTO>()
                {
                    Collation = _ignoreCaseCollation
                })).FirstOrDefaultAsync();

                return result == null ? 0 : result.Count;
            }
        }
    }
}
