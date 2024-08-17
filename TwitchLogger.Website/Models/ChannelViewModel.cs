using TwitchLogger.DTO;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Models
{
    public class ChannelViewModel
    {
        public ChannelDTO Channel { get; set; }
        public List<SubscriptionPlanInfo> Subscriptions { get; set; }
        public List<UserTopSubscription> TopSubscribers { get; set; }
        public bool IsOpt { get; set; }
    }
}
