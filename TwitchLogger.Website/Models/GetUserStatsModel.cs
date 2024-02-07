using System.ComponentModel.DataAnnotations;

namespace TwitchLogger.Website.Models
{
    public class GetUserStatsModel
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public string User { get; set; }

        public int Year { get; set; }
    }
}
