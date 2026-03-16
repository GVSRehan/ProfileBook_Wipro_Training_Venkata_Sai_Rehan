using System.ComponentModel.DataAnnotations;

namespace ProfileBook.API.Models
{
    public class Group
    {
        [Key]
        public int GroupId { get; set; }

        [Required]
        [StringLength(100)]
        public required string GroupName { get; set; }

        public string? GroupMembers { get; set; }
    }
}