﻿using TwitchLogger.DTO;

namespace TwitchLogger.Website.Interfaces
{
    public interface IChannelStatsRepository
    {
        public Task<IEnumerable<TwitchWordUserStatDTO>> GetTopUsers(string channelId, string word, int year = 0, int limit = 25);
        public Task<IEnumerable<TwitchWordUserStatDTO>> GetTopUserWords(string channelId, string user, int year = 0, int limit = 25);
        public Task<IEnumerable<TwitchWordStatDTO>> GetTopWords(string channelId, int year = 0, int limit = 100);
        public Task<IEnumerable<TwitchUserStatDTO>> GetTopChatters(string channelId, int year = 0, int limit = 100);
        public Task<ulong> GetWordCount(string channelId, string word, string user, int year = 0);
        public Task<TwitchUserStatDTO> GetUserStats(string channelId, string user, int year = 0);
        public Task<TwitchUserMessageTime> GetUserMessageTime(string channelId, string user);
        public Task<List<Tuple<string, int>>> GetUniqueSubscriptions(string channelId, long from, long to);
    }
}
