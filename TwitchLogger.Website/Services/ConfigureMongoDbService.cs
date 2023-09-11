using MongoDB.Driver;
using TwitchLogger.DTO;

namespace TwitchLogger.Website.Services
{
    public class ConfigureMongoDbService : IHostedService
    {
        private readonly DatabaseService _databaseService;

        public ConfigureMongoDbService(DatabaseService databaseService) => (_databaseService) = (databaseService);

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var accounts = _databaseService.GetAccountsCollection();
            await accounts.Indexes.CreateOneAsync(new CreateIndexModel<AccountDTO>(Builders<AccountDTO>.IndexKeys.Ascending(x => x.Login), new CreateIndexOptions() { Unique = true, Collation = new Collation("en", strength: CollationStrength.Secondary) }));

            var devices = _databaseService.GetDevicesCollection();
            await devices.Indexes.CreateOneAsync(new CreateIndexModel<DeviceDTO>(Builders<DeviceDTO>.IndexKeys.Ascending(x => x.AccountId)));
            await devices.Indexes.CreateOneAsync(new CreateIndexModel<DeviceDTO>(Builders<DeviceDTO>.IndexKeys.Ascending(x => x.Key), new CreateIndexOptions() { Unique = true }));

            var channels = _databaseService.GetChannelsCollection();
            await channels.Indexes.CreateOneAsync(new CreateIndexModel<ChannelDTO>(Builders<ChannelDTO>.IndexKeys.Ascending(x => x.UserId), new CreateIndexOptions() { Unique = true }));

            var twitchAccounts = _databaseService.GetTwitchAccountsCollection();
            await twitchAccounts.Indexes.CreateOneAsync(new CreateIndexModel<TwitchAccountDTO>(Builders<TwitchAccountDTO>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.Login), new CreateIndexOptions() { Unique = true }));
            await twitchAccounts.Indexes.CreateOneAsync(new CreateIndexModel<TwitchAccountDTO>(Builders<TwitchAccountDTO>.IndexKeys.Ascending(x => x.Login).Descending(x => x.RecordInsertTime), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) }));

            var twitchUsersMessageTime = _databaseService.GetTwitchUsersMessageTimeCollection();
            await twitchUsersMessageTime.Indexes.CreateOneAsync(new CreateIndexModel<TwitchUserMessageTime>(Builders<TwitchUserMessageTime>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.RoomId), new CreateIndexOptions() { Unique = true }));

            await (await channels.FindAsync(Builders<ChannelDTO>.Filter.Empty)).ForEachAsync(async x =>
            {
                await _databaseService.CreateIndexesForChannel(x.UserId);
            });
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
