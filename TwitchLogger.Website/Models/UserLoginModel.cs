using System.ComponentModel.DataAnnotations;

namespace TwitchLogger.Website.Models
{
    public class UserLoginModel
    {
        [Required]
        public string User { get; set; }
    }
}
