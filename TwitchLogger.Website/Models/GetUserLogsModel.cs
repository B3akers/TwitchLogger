using System.ComponentModel.DataAnnotations;

namespace TwitchLogger.Website.Models
{
    public class GetUserLogsModel
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public string User { get; set; }

        [Required]
        public string Date { get; set; }
    }
}
