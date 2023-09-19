
using TwitchLogger.DTO;

namespace TwitchLogger.Website.Interfaces
{
    public interface IChannelRepository
    {
        public Task<List<ChannelDTO>> GetChannels();
        public Task<ChannelDTO> AddChannelByUserId(string userId);
        public Task<ChannelDTO> GetChannelByUserId(string userId);
        public Task DeleteChannel(string channelId);
        public Task UpdateChannels();
        public Task<long> GetEstimatedCount();
        public Task<long> GetAllMessagesCount();
    }
}
