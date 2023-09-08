
using TwitchLogger.DTO;

namespace TwitchLogger.Website.Interfaces
{
    public interface IUserAuthentication
    {
        public Task<AccountDTO> GetAuthenticatedUser(HttpContext context);
        public Task AuthorizeForUser(HttpContext context, string accountId, bool permanent);
        public bool CheckCredentials(AccountDTO account, string password);
        public Task LogoutUser(HttpContext context);
    }
}
