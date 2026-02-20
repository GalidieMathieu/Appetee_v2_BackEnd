using Appetee.Application.Abstractions.Auth;
using Microsoft.AspNetCore.Identity;

namespace Appetee.Infrastructure.Auth
{
    public sealed class AspNetIdentityPasswordHasher : IPasswordHasher
    {
        private static readonly object DummyUser = new();
        private readonly PasswordHasher<object> _hasher = new();

        public string Hash(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required.", nameof(password));

            return _hasher.HashPassword(DummyUser, password);
        }

        public bool Verify(string password, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            if (string.IsNullOrWhiteSpace(passwordHash))
                return false;

            var result = _hasher.VerifyHashedPassword(DummyUser, passwordHash, password);

            // SuccessRehashNeeded means the password is correct, but hashing settings improved.
            // You can optionally re-hash and update the DB in that case.
            return result == PasswordVerificationResult.Success
                || result == PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
