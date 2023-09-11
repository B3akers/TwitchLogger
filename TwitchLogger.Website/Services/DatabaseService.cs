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
        private readonly IMongoCollection<TwitchUserMessageTime> _twitchUsersMessageTimeCollection;
        private readonly ConcurrentDictionary<string, IMongoCollection<TwitchWordUserStatDTO>> _twitchWordUserStatCollections;

        public DatabaseService(IConfiguration configuration)
        {
            _client = new MongoClient(configuration["Mongo:ConnectionString"]);
            _mongoDatabase = _client.GetDatabase(configuration["Mongo:DatabaseName"]);

            _accountsCollection = _mongoDatabase.GetCollection<AccountDTO>("accounts");
            _devicesCollection = _mongoDatabase.GetCollection<DeviceDTO>("devices");
            _channelsCollection = _mongoDatabase.GetCollection<ChannelDTO>("channels");
            _twitchAccountsCollection = _mongoDatabase.GetCollection<TwitchAccountDTO>("twitch_accounts");
            _twitchUsersMessageTimeCollection = _mongoDatabase.GetCollection<TwitchUserMessageTime>("twitch_users_message_time");

            _twitchWordUserStatCollections = new();
        }

        public MongoClient GetMongoClient()
        {
            return _client;
        }

        public IMongoDatabase GetMongoDatabase()
        {
            return _mongoDatabase;
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

        public IMongoCollection<TwitchUserMessageTime> GetTwitchUsersMessageTimeCollection()
        {
            return _twitchUsersMessageTimeCollection;
        }

        public ConcurrentDictionary<string, IMongoCollection<TwitchWordUserStatDTO>> GetTwitchWordUserStatCollections()
        {
            return _twitchWordUserStatCollections;
        }

        public IMongoCollection<TwitchWordUserStatDTO> GetTwitchWordUserStatCollectionForUser(string userId)
        {
            if (_twitchWordUserStatCollections.TryGetValue(userId, out var collection))
                return collection;

            return null;
        }

        public async Task CreateIndexesForChannel(string channelId)
        {
            {
                var collection = _mongoDatabase.GetCollection<TwitchWordUserStatDTO>($"twitch_word_user_stat_{channelId}");

                await collection.Indexes.CreateManyAsync(new[]
                {
                    new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.Word).Ascending(x => x.Year), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) }),
                    new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.Count).Ascending(x => x.Year)),
                    new CreateIndexModel<TwitchWordUserStatDTO>(Builders<TwitchWordUserStatDTO>.IndexKeys.Ascending(x => x.Word).Descending(x => x.Count).Ascending(x => x.Year), new CreateIndexOptions() { Collation = new Collation("en", strength: CollationStrength.Secondary) })
                });

                _twitchWordUserStatCollections[channelId] = collection;
            }
        }
    }
}
