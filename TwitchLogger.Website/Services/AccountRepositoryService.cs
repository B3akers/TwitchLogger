using DnsClient;
using MongoDB.Bson;
using MongoDB.Driver;
using TwitchLogger.DTO;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Services
{
    public class AccountRepositoryService : IAccountRepository
    {
        private readonly DatabaseService _databaseService;
        private readonly IPasswordHasher _passwordHasher;

        private Collation _ignoreCaseCollation;

        public AccountRepositoryService(DatabaseService databaseService, IPasswordHasher passwordHasher)
        {
            _databaseService = databaseService;
            _passwordHasher = passwordHasher;

            _ignoreCaseCollation = new Collation("en", strength: CollationStrength.Secondary);
        }

        public async Task<AccountDTO> CreateAccount(string login, string password, bool isAdmin = false, bool isModerator = false)
        {
            var accounts = _databaseService.GetAccountsCollection();

            var accountDTO = new AccountDTO();
            accountDTO.Login = login;
            accountDTO.Password = _passwordHasher.Hash(password);
            accountDTO.IsAdmin = isAdmin;
            accountDTO.IsModerator = isModerator;
            accountDTO.CreationTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            accountDTO.LastPasswordChange = accountDTO.CreationTime;

            await accounts.InsertOneAsync(accountDTO);

            return accountDTO;
        }

        public async Task DeleteAccount(string accountId)
        {
            var accounts = _databaseService.GetAccountsCollection();
            var devices = _databaseService.GetDevicesCollection();

            await accounts.DeleteOneAsync(x => x.Id == accountId);
            await devices.DeleteManyAsync(x => x.AccountId == accountId);
        }

        public async Task<AccountDTO> GetAccount(string accountId)
        {
            if (string.IsNullOrEmpty(accountId) || !ObjectId.TryParse(accountId, out _))
                return null;

            var accounts = _databaseService.GetAccountsCollection();
            return await (await accounts.FindAsync(x => x.Id == accountId)).FirstOrDefaultAsync();
        }

        public async Task<AccountDTO> GetAccountByLogin(string login)
        {
            if (string.IsNullOrEmpty(login))
                return null;

            var accounts = _databaseService.GetAccountsCollection();

            return await (await accounts.FindAsync(x => x.Login == login, new FindOptions<AccountDTO>() { Collation = _ignoreCaseCollation })).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<AccountDTO>> GetAccounts()
        {
            var accounts = _databaseService.GetAccountsCollection();
            return await (await accounts.FindAsync(Builders<AccountDTO>.Filter.Empty)).ToListAsync();
        }
    }
}
