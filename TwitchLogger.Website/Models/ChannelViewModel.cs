using TwitchLogger.DTO;

namespace TwitchLogger.Website.Models
{
    public class ChannelViewModel
    {
        public ChannelDTO Channel { get; set; }
        public List<Tuple<string, int>> Subscriptions { get; set; }
    }
}
