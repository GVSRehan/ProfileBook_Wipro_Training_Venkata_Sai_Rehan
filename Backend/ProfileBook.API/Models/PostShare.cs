using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.Models
{
    public class PostShare
    {
        [Key]
        public int PostShareId { get; set; }

        [Required]
        public int PostId { get; set; }

        [Required]
        public int SenderUserId { get; set; }

        [Required]
        public int RecipientUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
