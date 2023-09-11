using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLogger.SimpleGraphQL
{
    public class TwitchBadge
    {
        [JsonProperty("setID")]
        public string SetID { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("image1x")]
        public string Image1x { get; set; }
    }
}
