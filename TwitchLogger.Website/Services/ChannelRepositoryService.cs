using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.ObjectModel;
using System.Threading.Channels;
using TwitchLogger.DTO;
using TwitchLogger.SimpleGraphQL;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Services
{
    public class ChannelRepositoryService : IChannelRepository
    {
        private readonly DatabaseService _databaseService;

        public ChannelRepositoryService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<ChannelDTO>> GetChannels()
        {
            return await (await _databaseService.GetChannelsCollection().FindAsync(Builders<ChannelDTO>.Filter.Empty)).ToListAsync();
        }

        public async Task<ChannelDTO> AddChannelByUserId(string userId)
        {
            var channels = _databaseService.GetChannelsCollection();
            var channelInfo = await TwitchGraphQL.GetUserInfoById(userId);
            if (channelInfo == null)
                return null;

            var channel = new ChannelDTO()
            {
                UserId = channelInfo.Id,
                Login = channelInfo.Login,
                DisplayName = channelInfo.DisplayName,
                LogoUrl = channelInfo.ProfileImageURL,
                StartTrackingDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                MessageLastDate = 0,
                MessageCount = 0
            };

            await channels.InsertOneAsync(channel);
            await _databaseService.CreateIndexesForChannel(channel.UserId);

            return channel;
        }

        public async Task UpdateChannels()
        {
            var channels = _databaseService.GetChannelsCollection();
            List<string> channelsToUpdate = new List<string>();
            await (await channels.FindAsync(Builders<ChannelDTO>.Filter.Empty)).ForEachAsync(channel =>
            {
                channelsToUpdate.Add(channel.UserId);
            });

            var channelsInfo = await TwitchGraphQL.GetUsersInfoById(channelsToUpdate);
            List<UpdateOneModel<ChannelDTO>> bulkOps = new List<UpdateOneModel<ChannelDTO>>();

            foreach (var channelInfo in channelsInfo)
            {
                if (channelInfo == null)
                    continue;

                bulkOps.Add(new UpdateOneModel<ChannelDTO>(Builders<ChannelDTO>.Filter.Eq(x => x.UserId, channelInfo.Id), Builders<ChannelDTO>.Update.Set(x => x.Login, channelInfo.Login).Set(x => x.DisplayName, channelInfo.DisplayName).Set(x => x.LogoUrl, channelInfo.ProfileImageURL)));
            }

            if (bulkOps.Count > 0)
                await channels.BulkWriteAsync(bulkOps);
        }

        public async Task<ChannelDTO> GetChannelByUserId(string userId)
        {
            var channels = _databaseService.GetChannelsCollection();

            return await (await channels.FindAsync(x => x.UserId == userId)).FirstOrDefaultAsync();
        }

        public async Task DeleteChannel(string channelId)
        {
            if (string.IsNullOrEmpty(channelId))
                return;

            var channels = _databaseService.GetChannelsCollection();

            await channels.DeleteOneAsync(x => x.Id == channelId);
        }
    }
}
