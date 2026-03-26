using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class SendMessageDto
    {
        public int ReceiverId { get; set; }

        public int? GroupId { get; set; }

        [Required]
        [StringLength(500)]
        public required string MessageContent { get; set; }
    }
}
