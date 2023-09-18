using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLogger.DTO
{
    public class TwitchWordUserStatDTO
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string RoomId { get; set; }
        public string UserId { get; set; }
        public string Word { get; set; }
        public int Year { get; set; }
        public ulong Count { get; set; }
    }
}
