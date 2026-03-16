using Microsoft.AspNetCore.Mvc;
using ProfileBook.API.Data;
using ProfileBook.API.DTOs;
using ProfileBook.API.Models;
using System.Text.RegularExpressions;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly ProfileBookDbContext _context;

        public UserController(ProfileBookDbContext context)
        {
            _context = context;
        }

        // =========================
        // REGISTER USER
        // =========================
        [HttpPost("register")]
        public IActionResult Register(RegisterDto dto)
        {
            // Check if email already exists
            if (_context.Users.Any(u => u.Email == dto.Email))
                return BadRequest("Email already exists");

            // Username validation
            if (string.IsNullOrWhiteSpace(dto.Username) || dto.Username.Length < 3)
                return BadRequest("Username must be at least 3 characters");

            // Password match check
            if (dto.Password != dto.ConfirmPassword)
                return BadRequest("Passwords do not match");

            // Password complexity validation
            if (!ValidatePassword(dto.Password))
                return BadRequest("Password must contain 1 uppercase, 1 special character and be at least 8 characters");

            // Create user object
            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                Password = dto.Password,
                MobileNumber = dto.MobileNumber,
                Role = "User"
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok("User registered successfully");
        }

        // =========================
        // LOGIN USER
        // =========================
    [HttpPost("login")]
    public IActionResult Login(LoginDto dto)
    {
        Console.WriteLine("Email entered: " + dto.Email);
        Console.WriteLine("Password entered: " + dto.Password);

        var user = _context.Users.FirstOrDefault(u => u.Email == dto.Email);

        if (user == null)
            return Unauthorized("Email does not exist");

        if (user.Password != dto.Password)
            return Unauthorized("Password is incorrect");

        return Ok(new
        {
            user.UserId,
            user.Username,
            user.Email,
            user.Role
        });
    }
        // =========================
        // GET ALL USERS
        // =========================
        [HttpGet]
        public IActionResult GetUsers()
        {
            var users = _context.Users.ToList();
            return Ok(users);
        }

        // =========================
        // PASSWORD VALIDATION
        // =========================
        private bool ValidatePassword(string password)
        {
            // Minimum 8 characters
            // At least one uppercase
            // At least one special character
            var regex = new Regex(@"^(?=.*[A-Z])(?=.*[\W_]).{8,}$");
            return regex.IsMatch(password);
        }
    }
}