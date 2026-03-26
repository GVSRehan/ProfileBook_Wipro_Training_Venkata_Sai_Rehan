using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProfileBook.API.Data;
using ProfileBook.API.DTOs;
using ProfileBook.API.Helpers;
using ProfileBook.API.Models;
using ProfileBook.API.Services.Implementations;

namespace ProfileBook.API.Tests.Services;

public class UserServiceTests
{
    [Fact]
    public void Register_CreatesNormalizedUserWithHashedPassword()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var message = service.Register(new RegisterDto
        {
            Username = "  SaiRehan  ",
            Email = "  TEST@Example.com  ",
            Password = "Valid@123",
            ConfirmPassword = "Valid@123",
            MobileNumber = "9876543210"
        });

        var savedUser = Assert.Single(context.Users);
        Assert.Equal("User registered successfully", message);
        Assert.Equal("SaiRehan", savedUser.Username);
        Assert.Equal("test@example.com", savedUser.Email);
        Assert.NotEqual("Valid@123", savedUser.Password);
        Assert.Equal("User", savedUser.Role);
        Assert.True(PasswordHelper.VerifyPassword(savedUser, "Valid@123").IsSuccess);
    }

    [Fact]
    public void Login_AllowsUsernameIdentifier()
    {
        using var context = CreateContext();
        SeedUser(context, "sairehan", "sairehan@example.com", "9876543210", "Valid@123");
        var service = CreateService(context);

        var result = service.Login(new LoginDto
        {
            Identifier = "sairehan",
            Password = "Valid@123"
        });

        var token = result.GetType().GetProperty("Token")?.GetValue(result) as string;
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void Login_AllowsMobileNumberIdentifier()
    {
        using var context = CreateContext();
        SeedUser(context, "mobileuser", "mobile@example.com", "9998887776", "Valid@123");
        var service = CreateService(context);

        var result = service.Login(new LoginDto
        {
            Identifier = "9998887776",
            Password = "Valid@123"
        });

        var username = result.GetType().GetProperty("Username")?.GetValue(result) as string;
        Assert.Equal("mobileuser", username);
    }

    [Fact]
    public void ForgotPassword_CreatesSingleActiveResetToken()
    {
        using var context = CreateContext();
        SeedUser(context, "resetuser", "reset@example.com", "9998887775", "Valid@123");
        var service = CreateService(context);

        service.ForgotPassword(new ForgotPasswordDto { Email = "reset@example.com" });
        service.ForgotPassword(new ForgotPasswordDto { Email = "reset@example.com" });

        Assert.Equal(2, context.PasswordResetTokens.Count());
        Assert.Single(context.PasswordResetTokens.Where(token => !token.IsUsed));
    }

    private static ProfileBookDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProfileBookDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ProfileBookDbContext(options);
    }

    private static UserService CreateService(ProfileBookDbContext context)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SuperSecretKeyForTests1234567890",
                ["Jwt:Issuer"] = "ProfileBook.Tests",
                ["Jwt:Audience"] = "ProfileBook.Tests.Users"
            })
            .Build();

        return new UserService(context, configuration);
    }

    private static void SeedUser(
        ProfileBookDbContext context,
        string username,
        string email,
        string mobileNumber,
        string password)
    {
        var user = new User
        {
            Username = username,
            Email = email,
            Password = string.Empty,
            MobileNumber = mobileNumber,
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        user.Password = PasswordHelper.HashPassword(user, password);
        context.Users.Add(user);
        context.SaveChanges();
    }
}
