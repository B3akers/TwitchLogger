using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLogger.SimpleGraphQL
{
    public class TwitchLiveChannel
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Login { get; set; }
        public string Cursor { get; set; }
    }
}
