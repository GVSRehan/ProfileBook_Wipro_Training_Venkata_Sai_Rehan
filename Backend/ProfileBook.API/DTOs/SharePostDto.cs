using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class SharePostDto
    {
        [Required]
        public int RecipientUserId { get; set; }
    }
}
