using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.Models
{
    public class Report
    {
        [Key]
        public int ReportId { get; set; }

        [Required]
        public int ReportedUserId { get; set; }

        [Required]
        public int ReportingUserId { get; set; }

        [Required]
        [StringLength(500)]
        public required string Reason { get; set; }

        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;

        [StringLength(30)]
        public string Status { get; set; } = "Open";

        [StringLength(30)]
        public string? ActionTaken { get; set; }

        [StringLength(500)]
        public string? AdminNotes { get; set; }

        public DateTime? ResolvedAt { get; set; }

        [StringLength(100)]
        public string? ResolvedBy { get; set; }
    }
}
