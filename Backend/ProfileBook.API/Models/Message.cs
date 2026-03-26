using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.Models
{
    public class Message
    {
        [Key]
        public int MessageId { get; set; }

        [Required]
        public int SenderId { get; set; }

        public int? ReceiverId { get; set; }

        public int? GroupId { get; set; }

        [Required]
        [StringLength(500)]
        public required string MessageContent { get; set; }

        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
