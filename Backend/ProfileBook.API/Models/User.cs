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

        // Account creation and credential expiration tracking
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public DateTime? CredentialsExpireAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string? CreatedBy { get; set; } // Admin who created this user
        public bool IsMainAdmin { get; set; } = false; // True only for the main admin - never expires
    }
}