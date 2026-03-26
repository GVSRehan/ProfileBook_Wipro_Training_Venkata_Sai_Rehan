using ProfileBook.API.Data;
using ProfileBook.API.DTOs;
using ProfileBook.API.Helpers;
using ProfileBook.API.Models;
using ProfileBook.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ProfileBook.API.Services.Implementations
{
    public class AdminService : IAdminService
    {
        private readonly ProfileBookDbContext _context;

        public AdminService(ProfileBookDbContext context)
        {
            _context = context;
        }

        public object GetDashboardStats()
        {
            var totalUsers = _context.Users.Count();
            var pendingPosts = _context.Posts.Count(p => p.Status != null && p.Status.ToLower() == "pending");
            var reports = _context.Reports.Count();

            return new
            {
                totalUsers,
                pendingPosts,
                reports
            };
        }

        public List<object> GetUsers()
        {
            return _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.Email,
                    u.MobileNumber,
                    u.Role,
                    u.IsMainAdmin,
                    u.IsActive,
                    u.CreatedAt,
                    u.LastLoginAt,
                    u.CreatedBy,
                    CredentialsExpireAt = u.Role == "Admin" && !u.IsMainAdmin ? u.CredentialsExpireAt : null
                }).ToList<object>();
        }

        public object UpdateUser(int id, UpdateUserDto dto)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == id);
            if (user == null)
                throw new Exception("User not found");

            if (user.IsMainAdmin)
                throw new Exception("Main admin cannot be edited here");

            var normalizedUsername = IdentityNormalizer.NormalizeUsername(dto.Username);
            var normalizedUsernameLookup = IdentityNormalizer.NormalizeUsernameForLookup(dto.Username);
            var normalizedEmail = IdentityNormalizer.NormalizeEmail(dto.Email);
            var normalizedMobileNumber = IdentityNormalizer.NormalizeMobileNumber(dto.MobileNumber);

            if (_context.Users.Any(u => u.UserId != id && u.Email.Trim().ToLower() == normalizedEmail))
                throw new Exception("Email already exists");

            if (_context.Users.Any(u => u.UserId != id && u.Username.Trim().ToLower() == normalizedUsernameLookup))
                throw new Exception("Username already exists");

            if (_context.Users.Any(u => u.UserId != id && u.MobileNumber.Trim() == normalizedMobileNumber))
                throw new Exception("Mobile number already exists");

            user.Username = normalizedUsername;
            user.Email = normalizedEmail;
            user.MobileNumber = normalizedMobileNumber;
            user.IsActive = dto.IsActive ?? user.IsActive;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                if (!PasswordPolicyHelper.MeetsRequirements(dto.Password))
                    throw new Exception(PasswordPolicyHelper.ErrorMessage);

                user.Password = PasswordHelper.HashPassword(user, dto.Password);
            }

            _context.SaveChanges();

            return new
            {
                success = true,
                message = "User updated successfully",
                user = new
                {
                    user.UserId,
                    user.Username,
                    user.Email,
                    user.MobileNumber,
                    user.Role,
                    user.IsActive,
                    user.CreatedBy
                }
            };
        }

        public string DeleteUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null)
                throw new Exception("User not found");

            if (user.IsMainAdmin)
                throw new Exception("Cannot delete the main admin");

            var archiveTableExists = _context.Database
                .SqlQueryRaw<int>("SELECT TOP 1 CAST(1 AS int) AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DeletedUsers'")
                .Any();

            if (archiveTableExists)
            {
                var archived = new DeletedUser
                {
                    OriginalUserId = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    MobileNumber = user.MobileNumber ?? string.Empty,
                    Role = user.Role,
                    CreatedAt = user.CreatedAt,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = "Admin"
                };

                _context.DeletedUsers.Add(archived);
            }

            _context.Users.Remove(user);
            _context.SaveChanges();

            return archiveTableExists
                ? "User deleted and archived"
                : "User deleted successfully";
        }

        private int GetMinutesFromDuration(CreateAdminDto dto)
        {
            if (dto.DurationOption.HasValue)
            {
                return (int)dto.DurationOption.Value;
            }

            if (dto.ActiveMinutes.HasValue)
            {
                return dto.ActiveMinutes.Value;
            }

            return (int)AdminDurationOption.ThreeDays;
        }

        public object CreateAdmin(CreateAdminDto dto)
        {
            var normalizedUsername = IdentityNormalizer.NormalizeUsername(dto.Username);
            var normalizedUsernameLookup = IdentityNormalizer.NormalizeUsernameForLookup(dto.Username);
            var normalizedEmail = IdentityNormalizer.NormalizeEmail(dto.Email);
            var normalizedMobileNumber = IdentityNormalizer.NormalizeMobileNumber(dto.MobileNumber);

            if (_context.Users.Any(u => u.Username.Trim().ToLower() == normalizedUsernameLookup))
                throw new Exception("Username already exists");

            if (_context.Users.Any(u => u.Email.Trim().ToLower() == normalizedEmail))
                throw new Exception("Email already exists");

            if (_context.Users.Any(u => u.MobileNumber.Trim() == normalizedMobileNumber))
                throw new Exception("Mobile number already exists");

            if (!PasswordPolicyHelper.MeetsRequirements(dto.Password))
                throw new Exception(PasswordPolicyHelper.ErrorMessage);

            bool isMainAdmin = !_context.Users.Any(u => u.Role == "Admin");
            int minutes = GetMinutesFromDuration(dto);

            var user = new User
            {
                Username = normalizedUsername,
                Email = normalizedEmail,
                Password = string.Empty,
                MobileNumber = normalizedMobileNumber,
                Role = "Admin",
                CreatedAt = DateTime.UtcNow,
                CredentialsExpireAt = isMainAdmin ? null : DateTime.UtcNow.AddMinutes(minutes),
                IsActive = true,
                IsMainAdmin = isMainAdmin,
                CreatedBy = isMainAdmin ? "System" : "Admin"
            };
            user.Password = PasswordHelper.HashPassword(user, dto.Password!);

            _context.Users.Add(user);
            _context.SaveChanges();

            return new
            {
                success = true,
                userId = user.UserId,
                isMainAdmin,
                message = isMainAdmin
                    ? "Main Admin created successfully. This admin will never expire."
                    : $"Child Admin created successfully. Credentials expire at {user.CredentialsExpireAt}"
            };
        }

        public object CreateUser(CreateAdminDto dto)
        {
            var normalizedUsername = IdentityNormalizer.NormalizeUsername(dto.Username);
            var normalizedUsernameLookup = IdentityNormalizer.NormalizeUsernameForLookup(dto.Username);
            var normalizedEmail = IdentityNormalizer.NormalizeEmail(dto.Email);
            var normalizedMobileNumber = IdentityNormalizer.NormalizeMobileNumber(dto.MobileNumber);

            if (_context.Users.Any(u => u.Username.Trim().ToLower() == normalizedUsernameLookup))
                throw new Exception("Username already exists");

            if (_context.Users.Any(u => u.Email.Trim().ToLower() == normalizedEmail))
                throw new Exception("Email already exists");

            if (_context.Users.Any(u => u.MobileNumber.Trim() == normalizedMobileNumber))
                throw new Exception("Mobile number already exists");

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
                IsMainAdmin = false,
                CreatedBy = "Admin"
            };
            user.Password = PasswordHelper.HashPassword(user, dto.Password!);

            _context.Users.Add(user);
            _context.SaveChanges();

            return new
            {
                success = true,
                userId = user.UserId,
                message = "User created successfully"
            };
        }

        public object ExtendCredentials(int userId, int additionalMinutes)
        {
            if (additionalMinutes <= 0)
                throw new Exception("Additional minutes must be greater than zero");

            var user = _context.Users.Find(userId);
            if (user == null)
                throw new Exception("User not found");

            if (user.IsMainAdmin)
                return new
                {
                    success = true,
                    message = "Main admin credentials never expire.",
                    isMainAdmin = true
                };

            var currentExpiration = user.CredentialsExpireAt ?? DateTime.UtcNow;
            user.CredentialsExpireAt = currentExpiration > DateTime.UtcNow
                ? currentExpiration.AddMinutes(additionalMinutes)
                : DateTime.UtcNow.AddMinutes(additionalMinutes);
            user.IsActive = true;
            _context.SaveChanges();

            return new
            {
                success = true,
                message = $"Credentials extended. New expiration: {user.CredentialsExpireAt}",
                newExpiration = user.CredentialsExpireAt
            };
        }

        public List<object> GetExpiringCredentials(int daysAhead = 7)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(daysAhead);

            return _context.Users
                .Where(u => u.Role == "Admin" &&
                            !u.IsMainAdmin &&
                            u.CredentialsExpireAt.HasValue &&
                            u.CredentialsExpireAt.Value <= cutoffDate &&
                            u.CredentialsExpireAt.Value > DateTime.UtcNow)
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.Email,
                    u.MobileNumber,
                    u.Role,
                    u.CredentialsExpireAt,
                    DaysUntilExpiration = (int)((u.CredentialsExpireAt ?? DateTime.UtcNow) - DateTime.UtcNow).TotalDays,
                    HoursUntilExpiration = (int)((u.CredentialsExpireAt ?? DateTime.UtcNow) - DateTime.UtcNow).TotalHours
                })
                .OrderBy(u => u.CredentialsExpireAt)
                .ToList<object>();
        }

        public List<object> GetExpiredCredentials()
        {
            return _context.Users
                .Where(u => !u.IsMainAdmin &&
                            u.CredentialsExpireAt.HasValue &&
                            u.CredentialsExpireAt.Value < DateTime.UtcNow)
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.CredentialsExpireAt,
                    DaysExpired = u.CredentialsExpireAt.HasValue
                        ? (int?)(DateTime.UtcNow - u.CredentialsExpireAt.Value).TotalDays
                        : null
                })
                .ToList<object>();
        }

        public object DeactivateExpiredCredentials()
        {
            var expiredUsers = _context.Users
                .Where(u => u.Role == "Admin" &&
                            !u.IsMainAdmin &&
                            u.CredentialsExpireAt.HasValue &&
                            u.CredentialsExpireAt.Value < DateTime.UtcNow &&
                            u.IsActive)
                .ToList();

            foreach (var user in expiredUsers)
            {
                user.IsActive = false;
            }

            _context.SaveChanges();

            return new
            {
                success = true,
                message = $"{expiredUsers.Count} users deactivated due to expired credentials"
            };
        }

        public object SetMainAdmin(int userId)
        {
            var user = _context.Users.Find(userId);
            if (user == null)
                throw new Exception("User not found");

            if (user.Role != "Admin")
                throw new Exception("Only admins can be set as main admin");

            var allUsers = _context.Users.Where(u => u.Role == "Admin").ToList();
            foreach (var u in allUsers)
            {
                u.IsMainAdmin = false;
            }

            user.IsMainAdmin = true;
            user.IsActive = true;
            user.CredentialsExpireAt = null;
            _context.SaveChanges();

            return new
            {
                success = true,
                message = $"User {user.Username} is now the main admin. Main admin credentials will never expire.",
                user = new { userId = user.UserId, username = user.Username, email = user.Email }
            };
        }

        public object GetAdminProfile(int userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == userId && u.Role == "Admin");
            if (user == null)
                throw new Exception("Admin not found");

            return new
            {
                success = true,
                userId = user.UserId,
                username = user.Username,
                email = user.Email,
                mobileNumber = user.MobileNumber,
                role = user.Role,
                isMainAdmin = user.IsMainAdmin,
                createdAt = user.CreatedAt,
                lastLoginAt = user.LastLoginAt,
                credentialsExpireAt = user.CredentialsExpireAt,
                isActive = user.IsActive,
                createdBy = user.CreatedBy
            };
        }

        public bool IsMainAdmin(int userId)
        {
            return _context.Users.Any(u => u.UserId == userId && u.Role == "Admin" && u.IsMainAdmin);
        }

        public bool IsMainAdmin(string email)
        {
            var normalizedEmail = IdentityNormalizer.NormalizeEmail(email);
            return _context.Users.Any(u => u.Email.Trim().ToLower() == normalizedEmail && u.Role == "Admin" && u.IsMainAdmin);
        }
    }
}
