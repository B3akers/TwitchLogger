using TwitchLogger.DTO;

namespace TwitchLogger.Website.Interfaces
{
	public interface ITwitchAccountRepository
	{
		public Task<IEnumerable<TwitchAccountDTO>> GetTwitchAccounts(IEnumerable<string> userIds);
		public Task<IEnumerable<TwitchAccountDTO>> GetUserTwitchAccounts(string userId);
		public Task<TwitchAccountDTO> GetTwitchAccountByLogin(string login);
		public Task InsertTwitchAccountLogin(string userId, string userLogin);
		public Task<string> GetUserIdFromParam(string param);
		public Task<long> GetEstimatedUniqueCount();
	}
}
