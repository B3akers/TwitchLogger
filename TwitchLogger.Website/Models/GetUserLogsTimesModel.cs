using System.ComponentModel.DataAnnotations;

namespace TwitchLogger.Website.Models
{
    public class GetUserLogsTimesModel
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public string User { get; set; }
    }
}
