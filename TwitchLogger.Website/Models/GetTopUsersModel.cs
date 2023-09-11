using System.ComponentModel.DataAnnotations;

namespace TwitchLogger.Website.Models
{
    public class GetTopUsersModel
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public string Word { get; set; }

        public int Year { get; set; }
    }
}
