using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.DTOs
{
    public class CreateGroupDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public required string GroupName { get; set; }

        public List<int> MemberUserIds { get; set; } = new();
    }
}
