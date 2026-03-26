using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.Models
{
    public class AlertMessage
    {
        [Key]
        public int AlertMessageId { get; set; }

        [Required]
        public int AdminUserId { get; set; }

        [Required]
        [StringLength(500)]
        public required string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
