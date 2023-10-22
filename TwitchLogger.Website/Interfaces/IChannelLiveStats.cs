using Newtonsoft.Json.Linq;

namespace TwitchLogger.Website.Interfaces
{
    public interface IChannelLiveStats
    {
        public void ProcessMessage(JObject message);
    }
}
