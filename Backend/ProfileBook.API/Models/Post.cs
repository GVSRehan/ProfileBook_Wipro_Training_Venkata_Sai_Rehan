using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProfileBook.API.Models
{
    public class Post
    {
        [Key]
        public int PostId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(500)]
        public required string Content { get; set; }

        [StringLength(255)]
        public string? PostImage { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; }

        // Rejection reason
        [StringLength(500)]
        public string? RejectionReason { get; set; }
    }
}