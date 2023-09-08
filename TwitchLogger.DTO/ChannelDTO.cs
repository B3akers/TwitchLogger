using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TwitchLogger.DTO
{
    public class ChannelDTO
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string UserId { get; set; }
        public string Login { get; set; }
        public string DisplayName { get; set; }
        public string LogoUrl { get; set; }
        public long StartTrackingDate { get; set; }
        public long MessageLastDate { get; set; }
        public ulong MessageCount { get; set; }
    }
}
