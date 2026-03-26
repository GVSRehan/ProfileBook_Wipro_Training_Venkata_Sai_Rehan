namespace ProfileBook.API.DTOs
{
    public class LoginDto
    {
        public string? Email { get; set; }

        public string? Identifier { get; set; }

        public required string Password { get; set; }
    }
}
