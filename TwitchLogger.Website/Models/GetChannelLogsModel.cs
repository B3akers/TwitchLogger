using System.ComponentModel.DataAnnotations;

namespace TwitchLogger.Website.Models
{
    public class GetChannelLogsModel
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public long Date { get; set; }
    }
}
