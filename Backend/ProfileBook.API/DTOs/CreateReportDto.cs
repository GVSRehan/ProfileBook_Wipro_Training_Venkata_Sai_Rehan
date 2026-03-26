using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class CreateReportDto
    {
        [Required]
        public int ReportedUserId { get; set; }

        [Required]
        [StringLength(500)]
        public required string Reason { get; set; }
    }
}
