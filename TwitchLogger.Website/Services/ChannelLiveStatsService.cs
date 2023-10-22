using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Services
{

    public class ChannelLiveStatsService : IChannelLiveStats
    {
        private const int MESSAGE_TIME_STAMPS_TIME_MIN = 5;

        public class ChannelStats
        {
            public List<long> MessagesTimeStamps { get; set; }

            public ChannelStats()
            {
                MessagesTimeStamps = new();
            }
        }

        private readonly ConcurrentDictionary<string, ChannelStats> _channelsData;

        public ChannelLiveStatsService()
        {
            _channelsData = new ConcurrentDictionary<string, ChannelStats>();
        }

        public void ProcessMessage(JObject message)
        {
            var type = message["type"].ToString();

            if (type != "PRIVMSG")
                return;

            var roomId = message["roomId"].ToString();
            if (!_channelsData.TryGetValue(roomId, out var channel))
            {
                channel = new ChannelStats();

                if (!_channelsData.TryAdd(roomId, channel))
                    _channelsData.TryGetValue(roomId, out channel);
            }

            var timestampLimit = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (MESSAGE_TIME_STAMPS_TIME_MIN * 60);

            long timestamp = (long)message["timestamp"];

            channel.MessagesTimeStamps.Add(timestamp);
            channel.MessagesTimeStamps.RemoveAll(x => x < timestampLimit);

            /*
                messageObject["type"] = "PRIVMSG";
                messageObject["user"] = user;
                messageObject["roomId"] = roomId;
                messageObject["timestamp"] = unixCurrentDate;
                messageObject["message"] = args.Message;
                messageObject["words"] = JObject.FromObject(wordsMessage);
             */
        }
    }
}
