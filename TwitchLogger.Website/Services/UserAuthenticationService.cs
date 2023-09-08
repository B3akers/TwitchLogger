using MongoDB.Driver;
using TwitchLogger.DTO;
using TwitchLogger.Website.Interfaces;
using TwitchLogger.Website.Utility;

namespace TwitchLogger.Website.Services
{
    public class UserAuthenticationService : IUserAuthentication
    {
        private DatabaseService _databaseService;
        private IPasswordHasher _passwordHasher;
        private IAccountRepository _accountRepository;

        public UserAuthenticationService(DatabaseService databaseService, IPasswordHasher passwordHasher, IAccountRepository accountRepository)
        {
            _databaseService = databaseService;
            _passwordHasher = passwordHasher;
            _accountRepository = accountRepository;
        }

        public async Task AuthorizeForUser(HttpContext context, string accountId, bool permanent)
        {
            var account = await _accountRepository.GetAccount(accountId);
            if (account == null)
                return;

            if (permanent)
            {
                var devices = _databaseService.GetDevicesCollection();
                var device = new DeviceDTO() { AccountId = account.Id, Key = Randomizer.RandomString(Randomizer.Next(200, 255)), LastUse = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };

                await devices.InsertOneAsync(device);

                context.Response.Cookies.Append("deviceKey", device.Key, new CookieOptions() { Expires = DateTimeOffset.UtcNow.AddYears(1) });
                context.Response.Cookies.Append("userId", account.Id, new CookieOptions() { Expires = DateTimeOffset.UtcNow.AddYears(1) });
            }

            context.Session.SetString("passTimestamp", account.LastPasswordChange.ToString());
            context.Session.SetString("userId", accountId);
        }

        public bool CheckCredentials(AccountDTO account, string password)
        {
            return _passwordHasher.Check(account.Password, password);
        }

        public async Task LogoutUser(HttpContext context)
        {
            if (context.Request.Cookies.TryGetValue("deviceKey", out var deviceKey))
            {
                var devices = _databaseService.GetDevicesCollection();
                await devices.DeleteOneAsync(x => x.Key == deviceKey);
            }

            context.Session.Remove("passTimestamp");
            context.Session.Remove("userId");
            context.Response.Cookies.Delete("deviceKey");
            context.Response.Cookies.Delete("userId");
        }

        public async Task<AccountDTO> GetAuthenticatedUser(HttpContext context)
        {
            DeviceDTO accountDevice = null;
            var devices = _databaseService.GetDevicesCollection();
            var userId = context.Session.GetString("userId");
            if (string.IsNullOrEmpty(userId))
            {
                if (!context.Request.Cookies.TryGetValue("deviceKey", out string deviceKey))
                    return null;

                if (!context.Request.Cookies.TryGetValue("userId", out string cookieUserId))
                    return null;

                accountDevice = await (await devices.FindAsync(x => x.Key == deviceKey)).FirstOrDefaultAsync();
                if (accountDevice == null)
                    return null;

                if (cookieUserId != accountDevice.AccountId)
                    return null;

                context.Session.SetString("userId", accountDevice.AccountId);

                userId = accountDevice.AccountId;
            }

            var account = await _accountRepository.GetAccount(userId);

            if (account == null)
            {
                context.Session.Remove("userId");
                if (accountDevice != null)
                {
                    context.Response.Cookies.Delete("deviceKey");
                    context.Response.Cookies.Delete("userId");
                    await devices.DeleteOneAsync(x => x.Id == accountDevice.Id);
                }
            }
            else if (accountDevice != null)
            {
                await devices.UpdateOneAsync(x => x.Id == accountDevice.Id, Builders<DeviceDTO>.Update.Set(x => x.LastUse, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
                context.Session.SetString("passTimestamp", account.LastPasswordChange.ToString());
            }
            else
            {
                if (context.Session.GetString("passTimestamp") != account.LastPasswordChange.ToString())
                {
                    context.Session.Remove("passTimestamp");
                    context.Session.Remove("userId");
                    context.Response.Cookies.Delete("deviceKey");
                    context.Response.Cookies.Delete("userId");
                    return null;
                }
            }

            return account;
        }
    }
}
