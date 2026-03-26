using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class RespondFriendRequestDto
    {
        [Required]
        public required string Action { get; set; }
    }
}
