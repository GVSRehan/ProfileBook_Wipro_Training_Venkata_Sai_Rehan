using ProfileBook.API.Helpers;

namespace ProfileBook.API.Tests.Helpers;

public class PasswordPolicyHelperTests
{
    [Theory]
    [InlineData("Valid@123", true)]
    [InlineData("noupper@123", false)]
    [InlineData("NoSpecial123", false)]
    [InlineData("Sh0rt!", false)]
    public void MeetsRequirements_ReturnsExpectedResult(string password, bool expected)
    {
        var result = PasswordPolicyHelper.MeetsRequirements(password);

        Assert.Equal(expected, result);
    }
}
