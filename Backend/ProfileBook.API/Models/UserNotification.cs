using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.Models
{
    public class UserNotification
    {
        [Key]
        public int UserNotificationId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = "Info";

        [Required]
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? RelatedPostId { get; set; }

        public int? RelatedReportId { get; set; }
    }
}
