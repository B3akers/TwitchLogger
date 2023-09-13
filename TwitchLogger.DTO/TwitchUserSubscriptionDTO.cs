using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLogger.DTO
{
    public class TwitchUserSubscriptionDTO
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string RoomId;
        public string UserId;
        public string RecipientUserId;
        public string SubPlan;
        public string SubMessage;
        public int CumulativeMonths;
        public long Timestamp;
    }
}
