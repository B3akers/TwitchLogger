using MongoDB.Driver;
using TwitchLogger.DTO;
using TwitchLogger.SimpleGraphQL;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Services
{
    public class OptChannelRepositoryService : IOptChannelRepository
    {
        private readonly DatabaseService _databaseService;

        public OptChannelRepositoryService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<TwitchOptChannelDTO>> GetChannels()
        {
            return await (await _databaseService.GetOptChannelsColletion().FindAsync(Builders<TwitchOptChannelDTO>.Filter.Empty)).ToListAsync();
        }

        public async Task<TwitchOptChannelDTO> AddChannelByUserId(string userId)
        {
            var channels = _databaseService.GetOptChannelsColletion();
            var channelInfo = await TwitchGraphQL.GetUserInfoById(userId);
            if (channelInfo == null)
                return null;

            var channel = new TwitchOptChannelDTO()
            {
                TwitchId = channelInfo.Id,
                TwitchLogin = channelInfo.Login
            };

            await channels.InsertOneAsync(channel);

            return channel;
        }

        public async Task<TwitchOptChannelDTO> GetChannelByUserId(string userId)
        {
            var channels = _databaseService.GetOptChannelsColletion();

            return await (await channels.FindAsync(x => x.TwitchId == userId)).FirstOrDefaultAsync();
        }

        public async Task DeleteChannel(string channelId)
        {
            if (string.IsNullOrEmpty(channelId))
                return;

            var channels = _databaseService.GetOptChannelsColletion();

            await channels.DeleteOneAsync(x => x.Id == channelId);
        }
    }
}
