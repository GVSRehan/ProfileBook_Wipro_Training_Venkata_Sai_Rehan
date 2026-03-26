using System;

namespace ProfileBook.API.Models
{
    public class DeletedUser
    {
        public int DeletedUserId { get; set; }
        public int OriginalUserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
