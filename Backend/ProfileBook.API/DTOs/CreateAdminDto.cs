using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class CreateAdminDto
    {
        [Required, EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string? Username { get; set; }

        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Mobile number must be exactly 10 digits")]
        public string? MobileNumber { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        public string? Password { get; set; }

        public string? Description { get; set; }

        // Use DurationOption enum - OneDay, ThreeDays, OneWeek, TwoWeeks, OneMonth, ThreeMonths, SixMonths, OneYear
        public AdminDurationOption? DurationOption { get; set; }

        // Fallback: Direct minutes if DurationOption is not provided
        public int? ActiveMinutes { get; set; }
    }
}
