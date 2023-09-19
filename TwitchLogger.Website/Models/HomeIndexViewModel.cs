namespace TwitchLogger.Website.Models
{
    public class HomeIndexViewModel
    {
        public long ChannelsCount { get; set; }
        public long TwitchUniqueUsersCount { get; set; }
        public long TotalMessagesCount { get; set; }
        public Tuple<long, long> DatabaseSize { get; set; }
    }
}
