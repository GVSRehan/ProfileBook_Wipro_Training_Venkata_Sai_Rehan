using ProfileBook.API.Data;
using ProfileBook.API.DTOs;
using ProfileBook.API.Helpers;
using ProfileBook.API.Models;
using ProfileBook.API.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace ProfileBook.API.Services.Implementations
{
    public class UserService : IUserService
{
    private readonly ProfileBookDbContext _context;
    private readonly IConfiguration _configuration;

    public UserService(ProfileBookDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public string Register(RegisterDto dto)
    {
        var normalizedUsername = IdentityNormalizer.NormalizeUsername(dto.Username);
        var normalizedUsernameLookup = IdentityNormalizer.NormalizeUsernameForLookup(dto.Username);
        var normalizedEmail = IdentityNormalizer.NormalizeEmail(dto.Email);
        var normalizedMobileNumber = IdentityNormalizer.NormalizeMobileNumber(dto.MobileNumber);

        if (_context.Users.Any(u => u.Email.Trim().ToLower() == normalizedEmail))
            throw new Exception("Email already exists");

        if (_context.Users.Any(u => u.Username.Trim().ToLower() == normalizedUsernameLookup))
            throw new Exception("Username already exists");

        if (_context.Users.Any(u => u.MobileNumber.Trim() == normalizedMobileNumber))
            throw new Exception("Mobile number already exists");

        if (string.IsNullOrWhiteSpace(normalizedUsername) || normalizedUsername.Length < 3)
            throw new Exception("Username must be at least 3 characters");

        if (dto.Password != dto.ConfirmPassword)
            throw new Exception("Passwords do not match");

        if (!PasswordPolicyHelper.MeetsRequirements(dto.Password))
            throw new Exception(PasswordPolicyHelper.ErrorMessage);

        var user = new User
        {
            Username = normalizedUsername,
            Email = normalizedEmail,
            Password = string.Empty,
            MobileNumber = normalizedMobileNumber,
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            CredentialsExpireAt = null,
            IsActive = true,
            CreatedBy = "Self"
        };
        user.Password = PasswordHelper.HashPassword(user, dto.Password);

        _context.Users.Add(user);
        _context.SaveChanges();

        return "User registered successfully";
    }

    public object Login(LoginDto dto)
    {
        var identifier = IdentityNormalizer.NormalizeIdentifier(dto.Identifier ?? dto.Email);
        if (string.IsNullOrWhiteSpace(identifier))
            throw new Exception("Email, username, or mobile number is required");

        if (string.IsNullOrWhiteSpace(dto.Password))
            throw new Exception("Password is required");

        var normalizedIdentifier = identifier.ToLowerInvariant();
        var normalizedMobileNumber = IdentityNormalizer.NormalizeMobileNumber(identifier);

        var user = ResolveUserForLogin(normalizedIdentifier, normalizedMobileNumber);

        if (user == null)
            throw new Exception("Account not found. Use your email, username, or mobile number.");

        var passwordVerification = PasswordHelper.VerifyPassword(user, dto.Password);
        if (!passwordVerification.IsSuccess)
            throw new Exception("Password is incorrect");

        if (!user.IsActive)
            throw new Exception("Account is deactivated");

        // Only child admins expire. Normal users do not.
        if (user.Role == "Admin" && !user.IsMainAdmin)
        {
            if (user.CredentialsExpireAt.HasValue && user.CredentialsExpireAt.Value < DateTime.UtcNow)
                throw new Exception("Credentials have expired. Please contact admin to reset.");
        }

        if (passwordVerification.NeedsUpgrade)
        {
            user.Password = PasswordHelper.HashPassword(user, dto.Password);
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        _context.SaveChanges();

        var token = GenerateJwtToken(user);

        return new
        {
            user.UserId,
            user.Username,
            user.Email,
            user.Role,
            Token = token,
            CredentialsExpireAt = user.Role == "Admin" && !user.IsMainAdmin ? user.CredentialsExpireAt : null,
            DaysUntilExpiration = user.Role == "Admin" && !user.IsMainAdmin ?
                (user.CredentialsExpireAt.HasValue ?
                (int?)Math.Max(0, (user.CredentialsExpireAt.Value - DateTime.UtcNow).TotalDays) : null) : null,
            IsMainAdmin = user.IsMainAdmin
        };
    }

    public object ForgotPassword(ForgotPasswordDto dto)
    {
        var normalizedEmail = IdentityNormalizer.NormalizeEmail(dto.Email);
        var genericResponse = new
        {
            success = true,
            message = "If an account exists for that email, a reset code has been generated.",
            resetToken = (string?)null,
            expiresAt = (DateTime?)null
        };

        var user = _context.Users.FirstOrDefault(u => u.Email.Trim().ToLower() == normalizedEmail && u.IsActive);
        if (user == null)
        {
            return genericResponse;
        }

        var now = DateTime.UtcNow;
        var expiry = now.AddMinutes(15);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));

        var existingTokens = _context.PasswordResetTokens
            .Where(resetToken => resetToken.UserId == user.UserId && !resetToken.IsUsed)
            .ToList();

        foreach (var existingToken in existingTokens)
        {
            existingToken.IsUsed = true;
            existingToken.UsedAt = now;
        }

        _context.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.UserId,
            Token = token,
            CreatedAt = now,
            ExpiresAt = expiry,
            IsUsed = false
        });

        _context.SaveChanges();

        return new
        {
            success = true,
            message = "If an account exists for that email, a reset code has been generated.",
            resetToken = token,
            expiresAt = expiry
        };
    }

    public object ResetPassword(ResetPasswordDto dto)
    {
        var normalizedEmail = IdentityNormalizer.NormalizeEmail(dto.Email);
        var normalizedToken = dto.Token.Trim().ToUpperInvariant();

        if (dto.NewPassword != dto.ConfirmPassword)
            throw new Exception("Passwords do not match");

        if (!PasswordPolicyHelper.MeetsRequirements(dto.NewPassword))
            throw new Exception(PasswordPolicyHelper.ErrorMessage);

        var user = _context.Users.FirstOrDefault(u => u.Email.Trim().ToLower() == normalizedEmail && u.IsActive);
        if (user == null)
            throw new Exception("Reset token is invalid or expired");

        var resetToken = _context.PasswordResetTokens
            .Where(token => token.UserId == user.UserId && token.Token == normalizedToken)
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefault();

        if (resetToken == null || resetToken.IsUsed || resetToken.ExpiresAt < DateTime.UtcNow)
            throw new Exception("Reset token is invalid or expired");

        user.Password = PasswordHelper.HashPassword(user, dto.NewPassword);
        resetToken.IsUsed = true;
        resetToken.UsedAt = DateTime.UtcNow;

        var otherActiveTokens = _context.PasswordResetTokens
            .Where(token => token.UserId == user.UserId && !token.IsUsed && token.PasswordResetTokenId != resetToken.PasswordResetTokenId)
            .ToList();

        foreach (var activeToken in otherActiveTokens)
        {
            activeToken.IsUsed = true;
            activeToken.UsedAt = DateTime.UtcNow;
        }

        _context.SaveChanges();

        return new
        {
            success = true,
            message = "Password reset successfully"
        };
    }

    private User? ResolveUserForLogin(string normalizedIdentifier, string normalizedMobileNumber)
    {
        var emailMatches = _context.Users
            .Where(u => u.Email.Trim().ToLower() == normalizedIdentifier)
            .ToList();

        if (emailMatches.Count > 1)
            throw new Exception("Multiple accounts share this email. Please contact admin.");

        if (emailMatches.Count == 1)
            return emailMatches[0];

        var usernameMatches = _context.Users
            .Where(u => u.Username.Trim().ToLower() == normalizedIdentifier)
            .ToList();

        if (usernameMatches.Count > 1)
            throw new Exception("Multiple accounts share this username. Please contact admin.");

        if (usernameMatches.Count == 1)
            return usernameMatches[0];

        var mobileMatches = _context.Users
            .Where(u => u.MobileNumber.Trim() == normalizedMobileNumber)
            .ToList();

        if (mobileMatches.Count > 1)
            throw new Exception("Multiple accounts share this mobile number. Use email or username until admin fixes duplicate mobile numbers.");

        return mobileMatches.SingleOrDefault();
    }

    public List<object> GetUsers()
    {
        return _context.Users.Select(u => new
        {
            u.UserId,
            u.Username,
            u.Email,
            u.MobileNumber,
            u.Role,
            u.CreatedAt,
            u.LastLoginAt,
            CredentialsExpireAt = u.Role == "Admin" && !u.IsMainAdmin ? u.CredentialsExpireAt : null,
            u.IsActive,
            u.CreatedBy,
            DaysUntilExpiration = u.Role == "Admin" && !u.IsMainAdmin && u.CredentialsExpireAt.HasValue ?
                (int?)Math.Max(0, (u.CredentialsExpireAt.Value - DateTime.UtcNow).TotalDays) : null
        }).ToList<object>();
    }

    public string DeleteUser(int id)
    {
        var user = _context.Users.Find(id);
        if (user == null)
            throw new Exception("User not found");

        // Archive user before deletion
        var archived = new ProfileBook.API.Models.DeletedUser
        {
            OriginalUserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            MobileNumber = user.MobileNumber ?? string.Empty,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = null
        };

        _context.DeletedUsers.Add(archived);

        _context.Users.Remove(user);
        _context.SaveChanges();

        return "User deleted and archived successfully";
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("is_main_admin", user.IsMainAdmin.ToString().ToLowerInvariant())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured")));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    }
}
