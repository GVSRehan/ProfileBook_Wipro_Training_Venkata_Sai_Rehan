using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.Models
{
    public class Message
    {
        [Key]
        public int MessageId { get; set; }

        [Required]
        public int SenderId { get; set; }

        [Required]
        public int ReceiverId { get; set; }

        [Required]
        [StringLength(500)]
        public required string MessageContent { get; set; }

        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    }
}