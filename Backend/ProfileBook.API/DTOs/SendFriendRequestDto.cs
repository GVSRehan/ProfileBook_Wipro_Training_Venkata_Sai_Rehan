using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class SendFriendRequestDto
    {
        [Required]
        public int ReceiverId { get; set; }
    }
}
