using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.Models
{
    public class FriendRequest
    {
        [Key]
        public int FriendRequestId { get; set; }

        [Required]
        public int SenderId { get; set; }

        [Required]
        public int ReceiverId { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }
    }
}
