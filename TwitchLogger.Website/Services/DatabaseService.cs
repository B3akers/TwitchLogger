using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Concurrent;
using TwitchLogger.DTO;

namespace TwitchLogger.Website.Services
{
    public class DatabaseService
    {
        private MongoClient _client;
        private IMongoDatabase _mongoDatabase;

        private readonly IMongoCollection<AccountDTO> _accountsCollection;
        private readonly IMongoCollection<DeviceDTO> _devicesCollection;
        private readonly IMongoCollection<ChannelDTO> _channelsCollection;
        private readonly IMongoCollection<TwitchAccountDTO> _twitchAccountsCollection;
        private readonly IMongoCollection<TwitchAccountDTO> _twitchAccountsStaticCollection;
        private readonly IMongoCollection<TwitchUserMessageTime> _twitchUsersMessageTimeCollection;
        private readonly IMongoCollection<TwitchUserSubscriptionDTO> _twitchUserSubscriptionsCollection;
        private readonly IMongoCollection<TwitchUserStatDTO> _twitchUserStatsCollection;
        private readonly IMongoCollection<TwitchWordUserStatDTO> _twitchWordUserStatCollection;
        private readonly IMongoCollection<TwitchWordStatDTO> _twitchWordStatCollection;
        private readonly IMongoCollection<TwitchEmoteStatDTO> _twitchEmoteStatCollection;
        private readonly IMongoCollection<TwitchOptChannelDTO> _twitchOptChannelColletion;


        public DatabaseService(IConfiguration configuration)
        {
            _client = new MongoClient(configuration["Mongo:ConnectionString"]);
            _mongoDatabase = _client.GetDatabase(configuration["Mongo:DatabaseName"]);

            _accountsCollection = _mongoDatabase.GetCollection<AccountDTO>("accounts");
            _devicesCollection = _mongoDatabase.GetCollection<DeviceDTO>("devices");
            _channelsCollection = _mongoDatabase.GetCollection<ChannelDTO>("channels");
            _twitchAccountsCollection = _mongoDatabase.GetCollection<TwitchAccountDTO>("twitch_accounts");
            _twitchAccountsStaticCollection = _mongoDatabase.GetCollection<TwitchAccountDTO>("twitch_accounts_static");
            _twitchUsersMessageTimeCollection = _mongoDatabase.GetCollection<TwitchUserMessageTime>("twitch_users_message_time");
            _twitchUserSubscriptionsCollection = _mongoDatabase.GetCollection<TwitchUserSubscriptionDTO>("twitch_user_subscriptions");
            _twitchUserStatsCollection = _mongoDatabase.GetCollection<TwitchUserStatDTO>("twitch_user_stats");
            _twitchWordUserStatCollection = _mongoDatabase.GetCollection<TwitchWordUserStatDTO>("twitch_word_user_stats");
            _twitchWordStatCollection = _mongoDatabase.GetCollection<TwitchWordStatDTO>("twitch_word_stats");
            _twitchEmoteStatCollection = _mongoDatabase.GetCollection<TwitchEmoteStatDTO>("twitch_emote_stats");
            _twitchOptChannelColletion = _mongoDatabase.GetCollection<TwitchOptChannelDTO>("twitch_opt_channels");
        }

        public MongoClient GetMongoClient()
        {
            return _client;
        }

        public IMongoDatabase GetMongoDatabase()
        {
            return _mongoDatabase;
        }

        public IMongoCollection<TwitchOptChannelDTO> GetOptChannelsColletion()
        {
            return _twitchOptChannelColletion;
        }

        public IMongoCollection<AccountDTO> GetAccountsCollection()
        {
            return _accountsCollection;
        }

        public IMongoCollection<DeviceDTO> GetDevicesCollection()
        {
            return _devicesCollection;
        }

        public IMongoCollection<ChannelDTO> GetChannelsCollection()
        {
            return _channelsCollection;
        }

        public IMongoCollection<TwitchAccountDTO> GetTwitchAccountsCollection()
        {
            return _twitchAccountsCollection;
        }

        public IMongoCollection<TwitchAccountDTO> GetTwitchAccountsStaticCollection()
        {
            return _twitchAccountsStaticCollection;
        }

        public IMongoCollection<TwitchUserMessageTime> GetTwitchUsersMessageTimeCollection()
        {
            return _twitchUsersMessageTimeCollection;
        }

        public IMongoCollection<TwitchUserSubscriptionDTO> GetTwitchUserSubscriptionsCollection()
        {
            return _twitchUserSubscriptionsCollection;
        }

        public IMongoCollection<TwitchUserStatDTO> GetTwitchUserStatsCollection()
        {
            return _twitchUserStatsCollection;
        }

        public IMongoCollection<TwitchWordUserStatDTO> GetTwitchWordUserStatCollection()
        {
            return _twitchWordUserStatCollection;
        }

        public IMongoCollection<TwitchWordStatDTO> GetTwitchWordStatCollection()
        {
            return _twitchWordStatCollection;
        }

        public IMongoCollection<TwitchEmoteStatDTO> GetTwitchEmoteStatCollection()
        {
            return _twitchEmoteStatCollection;
        }

        public async Task<Tuple<long, long>> GetDatabaseStats()
        {
            var result = await _mongoDatabase.RunCommandAsync<BsonDocument>(new BsonDocument("dbStats", 1));

            return new Tuple<long, long>((long)(double)result["storageSize"], (long)(double)result["indexSize"]);
        }
    }
}
