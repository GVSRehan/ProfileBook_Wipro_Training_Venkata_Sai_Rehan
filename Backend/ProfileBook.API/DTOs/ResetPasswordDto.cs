using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class ResetPasswordDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        public required string Token { get; set; }

        [Required]
        [MinLength(8)]
        public required string NewPassword { get; set; }

        [Required]
        public required string ConfirmPassword { get; set; }
    }
}
