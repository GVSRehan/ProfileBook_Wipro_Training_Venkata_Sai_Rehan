using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProfileBook.API.DTOs;
using ProfileBook.API.Services.Interfaces;
using ProfileBook.API.Data;
using ProfileBook.API.Helpers;

namespace ProfileBook.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
    private readonly IUserService _service;
    private readonly ProfileBookDbContext _context;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserService service, ProfileBookDbContext context, ILogger<UserController> logger)
    {
        _service = service;
        _context = context;
        _logger = logger;
    }

    [HttpPost("register")]
    public IActionResult Register(RegisterDto dto)
    {
        try
        {
            return Ok(_service.Register(dto));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("login")]
    public IActionResult Login(LoginDto dto)
    {
        try
        {
            return Ok(_service.Login(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Login failed for identifier '{Identifier}'", dto.Identifier ?? dto.Email);

            if (IsAuthenticationFailure(ex.Message))
            {
                return Unauthorized(ex.Message);
            }

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Login could not be completed because the server returned an unexpected error."
            });
        }
    }

    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword(ForgotPasswordDto dto)
    {
        try
        {
            return Ok(_service.ForgotPassword(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forgot password failed for email '{Email}'", dto.Email);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("reset-password")]
    public IActionResult ResetPassword(ResetPasswordDto dto)
    {
        try
        {
            return Ok(_service.ResetPassword(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reset password failed for email '{Email}'", dto.Email);
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet]
    public IActionResult GetUsers()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return Unauthorized("Invalid user identity");

        var users = _context.Users
            .Where(u => u.IsActive && u.Role == "User")
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.Email,
                u.Role,
                u.ProfileImage
            })
            .ToList()
            .Where(u => u.UserId != currentUserId.Value)
            .ToList();

        return Ok(users);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUserProfile()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return Unauthorized("Invalid user identity");

        var user = await _context.Users
            .Where(u => u.UserId == currentUserId.Value && u.IsActive)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.Email,
                u.MobileNumber,
                u.Role,
                u.ProfileImage
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        return Ok(user);
    }

    [Authorize]
    [HttpPut("me")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UpdateCurrentUserProfile([FromForm] UpdateOwnProfileDto dto)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return Unauthorized("Invalid user identity");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId.Value && u.IsActive);
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var normalizedUsername = IdentityNormalizer.NormalizeUsername(dto.Username);
        if (string.IsNullOrWhiteSpace(normalizedUsername))
            return BadRequest(new { success = false, message = "Username is required" });

        var normalizedLookup = normalizedUsername.ToLowerInvariant();
        var usernameTaken = await _context.Users.AnyAsync(u =>
            u.UserId != currentUserId.Value &&
            u.Username != null &&
            u.Username.Trim().ToLower() == normalizedLookup);

        if (usernameTaken)
            return BadRequest(new { success = false, message = "Username already exists" });

        user.Username = normalizedUsername;

        if (dto.ProfileImageFile is { Length: > 0 })
        {
            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
            Directory.CreateDirectory(uploadsRoot);

            DeleteExistingProfileImage(user.ProfileImage);

            var safeFileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.ProfileImageFile.FileName)}";
            var filePath = Path.Combine(uploadsRoot, safeFileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await dto.ProfileImageFile.CopyToAsync(stream);

            user.ProfileImage = $"/uploads/profiles/{safeFileName}";
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Profile updated successfully",
            user = new
            {
                user.UserId,
                user.Username,
                user.Email,
                user.MobileNumber,
                user.Role,
                user.ProfileImage
            }
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public IActionResult DeleteUser(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return Unauthorized("Invalid user identity");

        var currentAdmin = _context.Users.FirstOrDefault(u => u.UserId == currentUserId.Value);
        if (currentAdmin?.IsMainAdmin != true)
            return StatusCode(StatusCodes.Status403Forbidden, "Only the main admin can delete users");

        try
        {
            return Ok(_service.DeleteUser(id));
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }

    private int? GetCurrentUserId()
    {
        return AuthHelper.GetCurrentUserId(User, _context, Request.Headers.Authorization);
    }

    private static void DeleteExistingProfileImage(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;

        var sanitizedRelativePath = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", sanitizedRelativePath);

        if (System.IO.File.Exists(absolutePath))
        {
            System.IO.File.Delete(absolutePath);
        }
    }

    private static bool IsAuthenticationFailure(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("required", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Account not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Password is incorrect", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Account is deactivated", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Credentials have expired", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Multiple accounts share", StringComparison.OrdinalIgnoreCase);
    }
    }
}
