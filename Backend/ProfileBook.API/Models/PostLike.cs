using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.Models
{
    public class PostLike
    {
        [Key]
        public int PostLikeId { get; set; }

        [Required]
        public int PostId { get; set; }

        [Required]
        public int UserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
