using TwitchLogger.DTO;

namespace TwitchLogger.Website.Interfaces
{
    public interface ITwitchAccountRepository
    {
        public Task<IEnumerable<TwitchAccountDTO>> GetTwitchAccounts(IEnumerable<string> userIds);
        public Task<TwitchAccountDTO> GetTwitchAccountByLogin(string login);
        public Task<long> GetEstimatedUniqueCount();
    }
}
