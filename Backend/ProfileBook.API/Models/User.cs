using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 3)]
        public required string Username { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public required string Email { get; set; }

        [Required]
        [StringLength(100)]
        public required string Password { get; set; }

        [Required]
        [RegularExpression(@"^[0-9]{10}$")]
        public required string MobileNumber { get; set; }

        public string Role { get; set; } = "User";

        public string? ProfileImage { get; set; }
    }
}