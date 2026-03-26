using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ProfileBook.API.DTOs
{
    public class UpdateOwnProfileDto
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public required string Username { get; set; }

        public IFormFile? ProfileImageFile { get; set; }
    }
}
