using System;
using System.Security.Cryptography;

namespace Shiftly.Server.Security
{
    /// <summary>
    /// מחלקת עזר להצפנה וגיבוב מאובטח של סיסמאות
    /// מיושם באמצעות PBKDF2 (Password-Based Key Derivation Function 2) עם HMAC-SHA512
    /// מגן מפני התקפות מילון וטבלאות קשת (Rainbow Tables) באמצעות Salt ייחודי לכל משתמש
    /// </summary>
    public static class PasswordSecurityHelper
    {
        private const int SaltByteSize = 128;
        private const int HashByteSize = 64;
        private const int Iterations = 100_000;

        /// <summary>
        /// יצירת גיבוב (Hash) ומלח (Salt) אקראי עבור סיסמה חדשה
        /// </summary>
        public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("הסיסמה אינה יכולה להיות ריקה.", nameof(password));

            // יצירת מלח קריפטוגרפי אקראי באורך 128 בייטים
            passwordSalt = RandomNumberGenerator.GetBytes(SaltByteSize);

            // גיבוב הסיסמה באמצעות PBKDF2 עם 100,000 סבבים
            passwordHash = Rfc2898DeriveBytes.Pbkdf2(
                password: password,
                salt: passwordSalt,
                iterations: Iterations,
                hashAlgorithm: HashAlgorithmName.SHA512,
                outputLength: HashByteSize);
        }

        /// <summary>
        /// אימות סיסמה מול הגיבוב והמלח השמורים במסד הנתונים
        /// </summary>
        public static bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            if (storedHash == null || storedHash.Length != HashByteSize)
                return false;

            if (storedSalt == null || storedSalt.Length != SaltByteSize)
                return false;

            // חישוב הגיבוב של הסיסמה שהוזנה עם המלח השמור
            byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                password: password,
                salt: storedSalt,
                iterations: Iterations,
                hashAlgorithm: HashAlgorithmName.SHA512,
                outputLength: HashByteSize);

            // השוואה בטוחה בזמן קבוע למניעת התקפות תזמון (Timing Attacks)
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }
    }
}
