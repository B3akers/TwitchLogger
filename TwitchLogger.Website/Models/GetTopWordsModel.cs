using System.ComponentModel.DataAnnotations;

namespace TwitchLogger.Website.Models
{
    public class GetTopWordsModel
    {
        [Required]
        public string Id { get; set; }

        public int Year { get; set; }
    }
}
