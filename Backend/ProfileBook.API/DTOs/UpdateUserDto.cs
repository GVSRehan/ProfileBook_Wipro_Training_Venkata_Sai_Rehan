using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class UpdateUserDto
    {
        [Required]
        [MinLength(3)]
        public required string Username { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Mobile number must be exactly 10 digits")]
        public required string MobileNumber { get; set; }

        public bool? IsActive { get; set; }

        [MinLength(8)]
        public string? Password { get; set; }
    }
}
