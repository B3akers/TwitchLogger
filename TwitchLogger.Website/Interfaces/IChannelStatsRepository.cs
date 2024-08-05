using TwitchLogger.DTO;

namespace TwitchLogger.Website.Interfaces
{
    public struct UserTopSubscription
    {
        public string _id { get; set; }
        public int Months { get; set; }
        public TwitchAccountDTO[] User { get; set; }
    }

    public struct SubscriptionPlanInfo
    {
        public string _id { get; set; }
        public int Count { get; set; }
    }

    public struct TwitchUserStatInfo
    {
        public TwitchUserStatDTO Stat { get; set; }
        public TwitchAccountDTO[] User { get; set; }
    };

    public interface IChannelStatsRepository
    {
        public Task<List<TwitchWordUserStatDTO>> GetTopUsers(string channelId, string word, int year = 0, int limit = 25);
        public Task<List<TwitchWordUserStatDTO>> GetTopUserWords(string channelId, string user, int year = 0, int limit = 25);
        public Task<List<TwitchWordStatDTO>> GetTopWords(string channelId, int year = 0, int limit = 100);
        public Task<List<TwitchWordStatDTO>> GetTopEmotes(string channelId, int year, string[] emotes);
        public Task<List<TwitchUserStatInfo>> GetTopChatters(string channelId, int year = 0, int limit = 100);
        public Task<ulong> GetWordCount(string channelId, string word, string user, int year = 0);
        public Task<TwitchUserStatDTO> GetUserStats(string channelId, string user, int year = 0);
        public Task<TwitchUserMessageTime> GetUserMessageTime(string channelId, string user);
        public Task<List<SubscriptionPlanInfo>> GetUniqueSubscriptions(string channelId, long from, long to);
        public Task<List<UserTopSubscription>> GetTopSubscriptions(string channelId);
    }
}
