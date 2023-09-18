using MongoDB.Driver;
using TwitchLogger.DTO;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Services
{
    public class TwitchAccountRepositoryService : ITwitchAccountRepository
    {
        private readonly DatabaseService _databaseService;
        public TwitchAccountRepositoryService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<TwitchAccountDTO> GetTwitchAccountByLogin(string login)
        {
            var twitchAccounts = _databaseService.GetTwitchAccountsCollection();

            return await (await twitchAccounts.FindAsync(x => x.Login == login, new FindOptions<TwitchAccountDTO>()
            {
                Collation = new Collation("en", strength: CollationStrength.Secondary),
                Sort = Builders<TwitchAccountDTO>.Sort.Descending(x => x.RecordInsertTime)
            })).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<TwitchAccountDTO>> GetTwitchAccounts(IEnumerable<string> userIds)
        {
            var twitchAccounts = _databaseService.GetTwitchAccountsStaticCollection();
            return await (await twitchAccounts.FindAsync(Builders<TwitchAccountDTO>.Filter.In(x => x.UserId, userIds))).ToListAsync();
        }
    }
}
