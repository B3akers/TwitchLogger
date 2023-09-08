using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TwitchLogger.DTO
{
    public class DeviceDTO
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        [BsonRepresentation(BsonType.ObjectId)]
        public string AccountId { get; set; }
        public string Key { get; set; }
        public long LastUse { get; set; }
    }
}
