
using TwitchLogger.DTO;

namespace TwitchLogger.Website.Interfaces
{
    public interface IAccountRepository
    {
        public Task<AccountDTO> GetAccount(string accountId);
        public Task<AccountDTO> GetAccountByLogin(string login);
        public Task<AccountDTO> CreateAccount(string login, string password, bool isAdmin = false, bool isModerator = false);
        public Task DeleteAccount(AccountDTO account);
    }
}
