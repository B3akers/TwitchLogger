using MongoDB.Driver;
using System.Threading.Channels;
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
            await devices.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<DeviceDTO>(Builders<DeviceDTO>.IndexKeys.Ascending(x => x.AccountId)),
                new CreateIndexModel<DeviceDTO>(Builders<DeviceDTO>.IndexKeys.Ascending(x => x.Key), new CreateIndexOptions() { Unique = true })
            });

            var channels = _databaseService.GetChannelsCollection();
            await channels.Indexes.CreateOneAsync(new CreateIndexModel<ChannelDTO>(Builders<ChannelDTO>.IndexKeys.Ascending(x => x.UserId), new CreateIndexOptions() { Unique = true }));
          
            var twitchAccountsStatic = _databaseService.GetTwitchAccountsStaticCollection();
            await twitchAccountsStatic.Indexes.CreateOneAsync(new CreateIndexModel<TwitchAccountDTO>(Builders<TwitchAccountDTO>.IndexKeys.Ascending(x => x.UserId), new CreateIndexOptions() { Unique = true }));

            var twitchAccounts = _databaseService.GetTwitchAccountsCollection();
            await twitchAccounts.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<TwitchAccountDTO>(Builders<TwitchAccountDTO>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.Login), new CreateIndexOptions() { Unique = true }),
                new CreateIndexModel<TwitchAccountDTO>(Builders<TwitchAccountDTO>.IndexKeys.Ascending(x => x.Login).Descending(x => x.RecordInsertTime), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) })
            });

            var twitchUsersMessageTime = _databaseService.GetTwitchUsersMessageTimeCollection();
            await twitchUsersMessageTime.Indexes.CreateOneAsync(new CreateIndexModel<TwitchUserMessageTime>(Builders<TwitchUserMessageTime>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.UserId), new CreateIndexOptions() { Unique = true }));

            var twitchUserSubscriptions = _databaseService.GetTwitchUserSubscriptionsCollection();
            await twitchUserSubscriptions.Indexes.CreateOneAsync(new CreateIndexModel<TwitchUserSubscriptionDTO>(Builders<TwitchUserSubscriptionDTO>.IndexKeys.Ascending(x => x.RoomId).Descending(x => x.Timestamp)));

            var twitchUserStats = _databaseService.GetTwitchUserStatsCollection();
            await twitchUserStats.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<TwitchUserStatDTO>(Builders<TwitchUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Ascending(x => x.UserId), new CreateIndexOptions() { Unique = true }),
                new CreateIndexModel<TwitchUserStatDTO>(Builders<TwitchUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Descending(x => x.Messages)),
            });

            var twitchWordUserStat = _databaseService.GetTwitchWordUserStatCollection();
            await twitchWordUserStat.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Ascending(x => x.UserId).Ascending(x => x.Word), new CreateIndexOptions() { Unique = true, Collation = new Collation("en", strength: CollationStrength.Secondary) }),
                new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Ascending(x => x.UserId).Descending(x => x.Count)),
                new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Ascending(x => x.Word).Descending(x => x.Count), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) })
            });

            var twitchWordStat = _databaseService.GetTwitchWordStatCollection();
            await twitchWordStat.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<TwitchWordStatDTO>(Builders<TwitchWordStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Ascending(x => x.Word), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary), Unique = true }),
                new CreateIndexModel<TwitchWordStatDTO>(Builders<TwitchWordStatDTO>.IndexKeys.Ascending(x => x.RoomId).Ascending(x => x.Year).Descending(x => x.Count))
            });
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
