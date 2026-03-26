using ProfileBook.API.Helpers;

namespace ProfileBook.API.Tests.Helpers;

public class IdentityNormalizerTests
{
    [Fact]
    public void NormalizeEmail_TrimAndLowercase_ReturnsExpectedValue()
    {
        var normalized = IdentityNormalizer.NormalizeEmail("  TeSt@Example.COM  ");

        Assert.Equal("test@example.com", normalized);
    }

    [Fact]
    public void NormalizeUsernameForLookup_TrimAndLowercase_ReturnsExpectedValue()
    {
        var normalized = IdentityNormalizer.NormalizeUsernameForLookup("  Sai Rehan  ");

        Assert.Equal("sai rehan", normalized);
    }
}
