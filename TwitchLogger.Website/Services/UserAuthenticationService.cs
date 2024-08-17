using MongoDB.Driver;
using System.Security.Claims;
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
        private IJwtTokenHandler _jwtTokenHandler;

        public UserAuthenticationService(DatabaseService databaseService, IPasswordHasher passwordHasher, IAccountRepository accountRepository, IJwtTokenHandler jwtTokenHandler)
        {
            _databaseService = databaseService;
            _passwordHasher = passwordHasher;
            _accountRepository = accountRepository;
            _jwtTokenHandler = jwtTokenHandler;
        }

        public void RegenerateTokenForUser(HttpContext context, AccountDTO account)
        {
            var token = _jwtTokenHandler.GenerateToken(new ClaimsIdentity(new Claim[]
            {
                  new Claim("Id", account.Id),
                  new Claim("IsAdmin", account.IsAdmin.ToString()),
                  new Claim("IsModerator", account.IsModerator.ToString())
            }),
            DateTime.UtcNow.AddMinutes(30));

            context.Response.Cookies.Append("sessionToken", token, new CookieOptions() { Expires = DateTimeOffset.UtcNow.AddMinutes(30) });
        }


        public async Task AuthorizeForUser(HttpContext context, string accountId, bool permanent)
        {
            var accounts = _databaseService.GetAccountsCollection();

            var account = await (await accounts.FindAsync(x => x.Id == accountId)).FirstOrDefaultAsync();
            if (account == null)
                return;

            if (permanent)
            {
                var devices = _databaseService.GetDevicesCollection();
                var device = new DeviceDTO() { AccountId = account.Id, Key = Randomizer.RandomString(100), LastUse = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };

                await devices.InsertOneAsync(device);

                context.Response.Cookies.Append("deviceKey", device.Key, new CookieOptions() { Expires = DateTimeOffset.UtcNow.AddYears(1) });
            }

            RegenerateTokenForUser(context, account);
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
            if (context.Request.Cookies.TryGetValue("sessionToken", out var session))
            {
                var claimsPrincipal = _jwtTokenHandler.ValidateToken(session);
                if (claimsPrincipal != null)
                {
                    AccountDTO accountDTO = new AccountDTO();

                    foreach (var claim in claimsPrincipal.Claims)
                    {
                        switch (claim.Type)
                        {
                            case "Id":
                                accountDTO.Id = claim.Value;
                                break;
                            case "IsAdmin":
                                accountDTO.IsAdmin = bool.Parse(claim.Value);
                                break;
                            case "IsModerator":
                                accountDTO.IsModerator = bool.Parse(claim.Value);
                                break;
                        }
                    }

                    return accountDTO;
                }
            }

            if (!context.Request.Cookies.TryGetValue("deviceKey", out string deviceKey))
                return null;

            var devices = _databaseService.GetDevicesCollection();
            DeviceDTO accountDevice = await (await devices.FindAsync(x => x.Key == deviceKey)).FirstOrDefaultAsync();
            if (accountDevice == null)
                return null;

            var accounts = _databaseService.GetAccountsCollection();
            var account = await (await accounts.FindAsync(x => x.Id == accountDevice.AccountId)).FirstOrDefaultAsync();
            if (account == null)
            {
                context.Response.Cookies.Delete("deviceKey");
                await devices.DeleteOneAsync(x => x.Id == accountDevice.Id);
                return null;
            }

            await devices.UpdateOneAsync(x => x.Id == accountDevice.Id, Builders<DeviceDTO>.Update.Set(x => x.LastUse, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

            RegenerateTokenForUser(context, account);

            return account;
        }
    }
}
