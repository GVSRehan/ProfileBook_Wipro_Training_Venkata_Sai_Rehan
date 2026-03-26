using System.Text.RegularExpressions;

namespace ProfileBook.API.Helpers
{
    public static class PasswordPolicyHelper
    {
        private static readonly Regex PasswordRegex = new(@"^(?=.*[A-Z])(?=.*[\W_]).{8,}$", RegexOptions.Compiled);

        public const string ErrorMessage = "Password must contain 1 uppercase, 1 special character and be at least 8 characters";

        public static bool MeetsRequirements(string? password)
        {
            return !string.IsNullOrWhiteSpace(password) && PasswordRegex.IsMatch(password);
        }
    }
}
