using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLogger.SimpleGraphQL
{
    public class TwitchUser
    {
        [JsonProperty("id")]
        public string Id {  get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("login")]
        public string Login { get; set; }

        [JsonProperty("profileImageURL")]
        public string ProfileImageURL { get; set; }
    }
}
