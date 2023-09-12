using System.ComponentModel.DataAnnotations;

namespace TwitchLogger.Website.Models
{
    public class AddAccountModel
    {
        [Required]
        [MinLength(4)]
        [MaxLength(25)]
        [RegularExpression("^[a-zA-Z0-9_]*$")]
        public string Login { get; set; }
        [Required]
        [MinLength(3)]
        [MaxLength(32)]
        public string Password { get; set; }
        public bool IsModerator { get; set; }
        public bool IsAdmin { get; set; }
    }
}
