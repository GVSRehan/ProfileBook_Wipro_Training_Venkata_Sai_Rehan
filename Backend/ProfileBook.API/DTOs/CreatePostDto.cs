using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ProfileBook.API.DTOs
{
    public class CreatePostDto
    {
        [Required]
        [StringLength(500)]
        public required string Content { get; set; }

        [StringLength(255)]
        public string? PostImage { get; set; }

        public IFormFile? PostImageFile { get; set; }
    }
}
