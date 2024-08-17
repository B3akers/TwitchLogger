using TwitchLogger.DTO;

namespace TwitchLogger.Website.Interfaces
{
    public interface IOptChannelRepository
    {
        public Task<List<TwitchOptChannelDTO>> GetChannels();
        public Task<TwitchOptChannelDTO> AddChannelByUserId(string userId);
        public Task<TwitchOptChannelDTO> GetChannelByUserId(string userId);
        public Task DeleteChannel(string channelId);

    }
}
