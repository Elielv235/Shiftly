using System;
using System.Security.Cryptography;
using System.Text;

namespace Shiftly.Server.Security
{
    /// <summary>
    /// Secure SHA-256 + Cryptographic Salt password hashing utility (SEC-M05).
    /// Passwords are never stored or compared in plaintext.
    /// </summary>
    public static class PasswordHasher
    {
        private const int DefaultSaltByteLength = 32; // 32 bytes -> 44 Base64 chars (fits TEXT(100))

        /// <summary>
        /// Generates a cryptographically strong random Base64 salt.
        /// </summary>
        public static string GenerateSalt(int byteLength = DefaultSaltByteLength)
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(byteLength);
            return Convert.ToBase64String(saltBytes);
        }

        /// <summary>
        /// Computes a SHA-256 Base64 hash from a plaintext password and its unique cryptographic salt.
        /// </summary>
        public static string HashPassword(string password, string salt)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or whitespace.", nameof(password));

            if (string.IsNullOrWhiteSpace(salt))
                throw new ArgumentException("Salt cannot be null or whitespace.", nameof(salt));

            byte[] saltBytes = Convert.FromBase64String(salt);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

            byte[] combined = new byte[saltBytes.Length + passwordBytes.Length];
            Buffer.BlockCopy(saltBytes, 0, combined, 0, saltBytes.Length);
            Buffer.BlockCopy(passwordBytes, 0, combined, saltBytes.Length, passwordBytes.Length);

            byte[] hashBytes = SHA256.HashData(combined);
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Generates a unique salt and computes the SHA-256 hash for a plaintext password.
        /// </summary>
        public static (string PasswordHash, string Salt) CreateHash(string password)
        {
            string salt = GenerateSalt();
            string hash = HashPassword(password, salt);
            return (hash, salt);
        }

        /// <summary>
        /// Verifies a plaintext password against a stored SHA-256 hash and salt using constant-time comparison.
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            if (string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(storedHash) ||
                string.IsNullOrWhiteSpace(storedSalt))
            {
                return false;
            }

            try
            {
                string computedHash = HashPassword(password, storedSalt);
                byte[] computedBytes = Convert.FromBase64String(computedHash);
                byte[] storedBytes = Convert.FromBase64String(storedHash);

                return computedBytes.Length == storedBytes.Length &&
                       CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}

