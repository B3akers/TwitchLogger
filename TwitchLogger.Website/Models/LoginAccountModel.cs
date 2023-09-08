using System.ComponentModel.DataAnnotations;

namespace TwitchLogger.Website.Models
{
    public class LoginAccountModel
    {
        [Required]
        [MinLength(3)]
        [MaxLength(32)]
        [RegularExpression("^[a-zA-Z0-9_]*$")]
        public string Login { get; set; }

        [Required]
        [MinLength(6)]
        [MaxLength(64)]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }
}
