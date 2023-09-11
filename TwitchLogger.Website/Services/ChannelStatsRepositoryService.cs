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
            var wordUserStats = _databaseService.GetTwitchWordUserStatCollectionForUser(channelId);
            if (wordUserStats == null)
                return Enumerable.Empty<TwitchWordUserStatDTO>();

            return await (await wordUserStats.FindAsync(x => x.UserId == user && x.Year == year, new FindOptions<TwitchWordUserStatDTO>()
            {
                Collation = _ignoreCaseCollation,
                Limit = limit,
                Sort = Builders<TwitchWordUserStatDTO>.Sort.Descending(x => x.Count)
            })).ToListAsync();
        }

        public async Task<IEnumerable<TwitchWordUserStatDTO>> GetTopUsers(string channelId, string word, int year, int limit)
        {
            var wordUserStats = _databaseService.GetTwitchWordUserStatCollectionForUser(channelId);
            if (wordUserStats == null)
                return Enumerable.Empty<TwitchWordUserStatDTO>();

            return await (await wordUserStats.FindAsync(x => x.Word == word && x.Year == year, new FindOptions<TwitchWordUserStatDTO>()
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
    }
}
