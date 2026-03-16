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
        public required User User { get; set; }
    }
}