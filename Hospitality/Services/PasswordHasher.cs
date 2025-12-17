namespace Hospitality.Services
{
    /// <summary>
    /// Service for hashing and verifying passwords using BCrypt
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>
        /// Hashes a plain text password using BCrypt
        /// </summary>
        /// <param name="password">The plain text password to hash</param>
        /// <returns>The hashed password</returns>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            }

            // BCrypt automatically generates a salt and includes it in the hash
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }

        /// <summary>
        /// Verifies a plain text password against a hashed password
        /// </summary>
        /// <param name="password">The plain text password to verify</param>
        /// <param name="hashedPassword">The hashed password to compare against</param>
        /// <returns>True if the password matches, false otherwise</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(hashedPassword))
            {
                return false;
            }

            try
            {
                // BCrypt handles the salt extraction and comparison automatically
                return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            }
            catch
            {
                // If verification fails (e.g., invalid hash format), return false
                return false;
            }
        }

        /// <summary>
        /// Checks if a password string appears to be a BCrypt hash
        /// This is useful for migration scenarios where some passwords might still be plain text
        /// </summary>
        /// <param name="password">The password string to check</param>
        /// <returns>True if it appears to be a BCrypt hash, false otherwise</returns>
        public static bool IsHashed(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            // BCrypt hashes start with $2a$, $2b$, $2x$, or $2y$ followed by the work factor
            return password.StartsWith("$2") && password.Length > 20;
        }
    }
}

