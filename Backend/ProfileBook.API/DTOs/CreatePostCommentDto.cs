using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class CreatePostCommentDto
    {
        [Required]
        [StringLength(500)]
        public required string CommentText { get; set; }
    }
}
