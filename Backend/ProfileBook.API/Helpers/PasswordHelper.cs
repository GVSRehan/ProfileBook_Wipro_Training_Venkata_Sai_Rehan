using Microsoft.AspNetCore.Identity;
using ProfileBook.API.Models;

namespace ProfileBook.API.Helpers
{
    public readonly record struct PasswordVerificationOutcome(bool IsSuccess, bool NeedsUpgrade);

    public static class PasswordHelper
    {
        private static readonly PasswordHasher<User> PasswordHasher = new();

        public static string HashPassword(User user, string plainTextPassword)
        {
            return PasswordHasher.HashPassword(user, plainTextPassword);
        }

        public static PasswordVerificationOutcome VerifyPassword(User user, string providedPassword)
        {
            if (string.IsNullOrWhiteSpace(user.Password) || string.IsNullOrEmpty(providedPassword))
            {
                return new PasswordVerificationOutcome(false, false);
            }

            PasswordVerificationResult verificationResult;
            try
            {
                verificationResult = PasswordHasher.VerifyHashedPassword(user, user.Password, providedPassword);
            }
            catch (FormatException)
            {
                verificationResult = PasswordVerificationResult.Failed;
            }

            if (verificationResult == PasswordVerificationResult.Success)
            {
                return new PasswordVerificationOutcome(true, false);
            }

            if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                return new PasswordVerificationOutcome(true, true);
            }

            if (string.Equals(user.Password, providedPassword, StringComparison.Ordinal))
            {
                return new PasswordVerificationOutcome(true, true);
            }

            return new PasswordVerificationOutcome(false, false);
        }
    }
}
