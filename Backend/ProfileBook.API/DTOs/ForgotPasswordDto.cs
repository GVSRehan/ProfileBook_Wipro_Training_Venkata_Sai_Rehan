using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }
}
