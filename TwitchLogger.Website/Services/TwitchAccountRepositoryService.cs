using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using TwitchLogger.DTO;
using TwitchLogger.SimpleGraphQL;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Services
{
    public class TwitchAccountRepositoryService : ITwitchAccountRepository
    {
        private readonly DatabaseService _databaseService;
        private readonly IMemoryCache _memoryCache;
        public TwitchAccountRepositoryService(DatabaseService databaseService, IMemoryCache memoryCache)
        {
            _databaseService = databaseService;
            _memoryCache = memoryCache;
        }

        public async Task<long> GetEstimatedUniqueCount()
        {
            return await _databaseService.GetTwitchAccountsStaticCollection().EstimatedDocumentCountAsync();
        }

        public async Task<TwitchAccountDTO> GetTwitchAccountByLogin(string login)
        {
            var twitchAccounts = _databaseService.GetTwitchAccountsCollection();

            return await (await twitchAccounts.FindAsync(x => x.Login == login, new FindOptions<TwitchAccountDTO>()
            {
                Collation = new Collation("en", strength: CollationStrength.Secondary),
                Sort = Builders<TwitchAccountDTO>.Sort.Descending(x => x.RecordInsertTime),
                Limit = 1,
            })).FirstOrDefaultAsync();
        }

        public async Task InsertTwitchAccountLogin(string userId, string userLogin)
        {
            var twitchAccounts = _databaseService.GetTwitchAccountsCollection();
            await twitchAccounts.UpdateOneAsync(x => x.UserId == userId && x.Login == userLogin, Builders<TwitchAccountDTO>.Update.Set(x => x.DisplayName, userLogin).SetOnInsert(x => x.RecordInsertTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), new UpdateOptions() { IsUpsert = true });

            var twitchStaticAccounts = _databaseService.GetTwitchAccountsStaticCollection();
            await twitchStaticAccounts.UpdateOneAsync(x => x.UserId == userId, Builders<TwitchAccountDTO>.Update.Set(x => x.Login, userLogin).Set(x => x.DisplayName, userLogin).Set(x => x.RecordInsertTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), new UpdateOptions() { IsUpsert = true });
        }

        public async Task<IEnumerable<TwitchAccountDTO>> GetTwitchAccounts(IEnumerable<string> userIds)
        {
            var twitchAccounts = _databaseService.GetTwitchAccountsStaticCollection();
            return await (await twitchAccounts.FindAsync(Builders<TwitchAccountDTO>.Filter.In(x => x.UserId, userIds))).ToListAsync();
        }

        public async Task<string> GetUserIdFromParam(string param)
        {
            if (string.IsNullOrEmpty(param))
                return param;

            if (param.StartsWith("id:"))
                return param.Substring(3);

            var user = await GetTwitchAccountByLogin(param);
            if (user == null)
            {
                var userId = await _memoryCache.GetOrCreateAsync($"user_{param}", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
                    return await TwitchGraphQL.GetUserID(param);
                });

                if (string.IsNullOrEmpty(userId))
                    return string.Empty;

                await InsertTwitchAccountLogin(userId, param);

                return userId;
            }

            return user.UserId;
        }

        public async Task<IEnumerable<TwitchAccountDTO>> GetUserTwitchAccounts(string userId)
        {
            var twitchAccounts = _databaseService.GetTwitchAccountsCollection();

            return await (await twitchAccounts.FindAsync(x => x.UserId == userId)).ToListAsync();
        }
    }
}
