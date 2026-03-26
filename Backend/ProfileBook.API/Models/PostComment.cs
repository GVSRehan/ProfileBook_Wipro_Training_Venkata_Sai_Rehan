using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.Models
{
    public class PostComment
    {
        [Key]
        public int PostCommentId { get; set; }

        [Required]
        public int PostId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(500)]
        public required string CommentText { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
