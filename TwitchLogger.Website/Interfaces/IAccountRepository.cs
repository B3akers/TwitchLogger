
using TwitchLogger.DTO;

namespace TwitchLogger.Website.Interfaces
{
    public interface IAccountRepository
    {
        public Task<AccountDTO> GetAccount(string accountId);
        public Task<AccountDTO> GetAccountByLogin(string login);
        public Task<AccountDTO> CreateAccount(string login, string password, bool isAdmin = false, bool isModerator = false);
        public Task DeleteAccount(string accountId);

        //TODO We should use limit, cursor (there will be a maximum of 5 users so it is not needed at the moment)
        public Task<IEnumerable<AccountDTO>> GetAccounts();
    }
}
