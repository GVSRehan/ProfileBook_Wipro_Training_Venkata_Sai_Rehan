using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class TakeReportActionDto
    {
        [Required]
        [RegularExpression("^(dismiss|warn|deactivate)$", ErrorMessage = "Action must be dismiss, warn, or deactivate")]
        public required string Action { get; set; }

        [StringLength(500)]
        public string? AdminNotes { get; set; }
    }
}
