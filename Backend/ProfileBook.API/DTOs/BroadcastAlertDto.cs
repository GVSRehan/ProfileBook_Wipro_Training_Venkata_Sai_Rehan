using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class BroadcastAlertDto
    {
        [Required]
        [StringLength(500)]
        public required string Content { get; set; }
    }
}
