using System.ComponentModel.DataAnnotations;

namespace TwitchLogger.Website.Models
{
    public class IdModel
    {
        [RegularExpression("^[a-f\\d]{24}$")]
        [Required]
        public string Id { get; set; }
    }
}
