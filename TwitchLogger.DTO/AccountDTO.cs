using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TwitchLogger.DTO
{
    public class AccountDTO
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public long CreationTime { get; set; }
        public long LastPasswordChange { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsModerator { get; set; }
    }
}
