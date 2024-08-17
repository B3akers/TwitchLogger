using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLogger.SimpleGraphQL
{
    public class TwitchEmote
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("setID")]
        public string SetID { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("assetType")]
        public string AssetType { get; set; }
    }
}
