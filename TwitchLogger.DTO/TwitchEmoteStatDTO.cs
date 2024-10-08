﻿using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLogger.DTO
{
    public class TwitchEmoteStatDTO
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string RoomId { get; set; }
        public string Emote { get; set; }
        public string EmoteType { get; set; }
        public string Url { get; set; }
        public int Year { get; set; }
        public ulong Count { get; set; }
    }
}
