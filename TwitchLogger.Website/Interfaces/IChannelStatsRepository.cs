﻿using TwitchLogger.DTO;

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

    public interface IChannelStatsRepository
    {
        public Task<IEnumerable<TwitchWordUserStatDTO>> GetTopUsers(string channelId, string word, int year = 0, int limit = 25);
        public Task<IEnumerable<TwitchWordUserStatDTO>> GetTopUserWords(string channelId, string user, int year = 0, int limit = 25);
        public Task<IEnumerable<TwitchWordStatDTO>> GetTopWords(string channelId, int year = 0, int limit = 100);
        public Task<IEnumerable<TwitchUserStatDTO>> GetTopChatters(string channelId, int year = 0, int limit = 100);
        public Task<ulong> GetWordCount(string channelId, string word, string user, int year = 0);
        public Task<TwitchUserStatDTO> GetUserStats(string channelId, string user, int year = 0);
        public Task<TwitchUserMessageTime> GetUserMessageTime(string channelId, string user);
        public Task<List<SubscriptionPlanInfo>> GetUniqueSubscriptions(string channelId, long from, long to);
        public Task<List<UserTopSubscription>> GetTopSubscriptions(string channelId);
    }
}
