namespace ProfileBook.API.Helpers
{
    public static class IdentityNormalizer
    {
        public static string NormalizeEmail(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        public static string NormalizeUsername(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        public static string NormalizeUsernameForLookup(string? value)
        {
            return NormalizeUsername(value).ToLowerInvariant();
        }

        public static string NormalizeMobileNumber(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        public static string NormalizeIdentifier(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
