using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class RegisterDto
    {
        [Required]
        [MinLength(3)]
        public required string Username { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [MinLength(8)]
        public required string Password { get; set; }

        [Required]
        public required string ConfirmPassword { get; set; }

        [Required]
        [RegularExpression(@"^[0-9]{10}$")]
        public required string MobileNumber { get; set; }
    }
}