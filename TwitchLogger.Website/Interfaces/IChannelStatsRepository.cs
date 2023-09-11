using TwitchLogger.DTO;

namespace TwitchLogger.Website.Interfaces
{
    public interface IChannelStatsRepository
    {
        public Task<IEnumerable<TwitchWordUserStatDTO>> GetTopUsers(string channelId, string word, int year = 0, int limit = 25);
        public Task<IEnumerable<TwitchWordUserStatDTO>> GetTopUserWords(string channelId, string user, int year = 0, int limit = 25);
        public Task<TwitchUserMessageTime> GetUserMessageTime(string channelId, string user); 
    }
}
