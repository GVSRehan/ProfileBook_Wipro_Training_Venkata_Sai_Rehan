using ProfileBook.API.Helpers;
using ProfileBook.API.Models;

namespace ProfileBook.API.Tests.Helpers;

public class PasswordHelperTests
{
    [Fact]
    public void HashPassword_AndVerifyPassword_SucceedsWithoutUpgrade()
    {
        var user = CreateUser();
        user.Password = PasswordHelper.HashPassword(user, "Valid@123");

        var result = PasswordHelper.VerifyPassword(user, "Valid@123");

        Assert.True(result.IsSuccess);
        Assert.False(result.NeedsUpgrade);
    }

    [Fact]
    public void VerifyPassword_WithLegacyPlainTextPassword_SucceedsAndRequestsUpgrade()
    {
        var user = CreateUser();
        user.Password = "Legacy@123";

        var result = PasswordHelper.VerifyPassword(user, "Legacy@123");

        Assert.True(result.IsSuccess);
        Assert.True(result.NeedsUpgrade);
    }

    private static User CreateUser()
    {
        return new User
        {
            Username = "tester",
            Email = "tester@example.com",
            Password = string.Empty,
            MobileNumber = "9876543210"
        };
    }
}
