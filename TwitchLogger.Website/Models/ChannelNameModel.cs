using System.ComponentModel.DataAnnotations;

namespace TwitchLogger.Website.Models
{
    public class ChannelNameModel
    {
        [Required]
        [MinLength(4)]
        [MaxLength(25)]
        [RegularExpression("^[a-zA-Z0-9_ ]*$")]
        public string Login { get; set; }
    }
}
