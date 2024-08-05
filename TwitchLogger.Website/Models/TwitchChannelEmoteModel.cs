namespace TwitchLogger.Website.Models
{

    public class TwitchChannelEmoteModel
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }

    public class TwitchChannelEmoteSetModel
    {
        public string[] EmotesName { get; set; }
        public TwitchChannelEmoteModel[] Emotes { get; set; }
    }
}
