using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Services
{
    public class PasswordHasher : IPasswordHasher
    {
        public bool Check(string hash, string password)
        {
            if (string.IsNullOrEmpty(hash))
                return false;

            byte[] salt = new byte[128 / 8];

            byte[] decodedHashedPassword = Convert.FromBase64String(hash);
            Buffer.BlockCopy(decodedHashedPassword, 0, salt, 0, salt.Length);

            byte[] expectedSubkey = new byte[256 / 8];
            Buffer.BlockCopy(decodedHashedPassword, salt.Length, expectedSubkey, 0, expectedSubkey.Length);

            byte[] actualSubkey = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA1, 1000, 256 / 8);

            return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
        }

        public string Hash(string password)
        {
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] subkey = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA1,
            iterationCount: 1000,
            numBytesRequested: 256 / 8);

            byte[] finalPassword = new byte[salt.Length + subkey.Length];
            Buffer.BlockCopy(salt, 0, finalPassword, 0, salt.Length);
            Buffer.BlockCopy(subkey, 0, finalPassword, salt.Length, subkey.Length);

            return Convert.ToBase64String(finalPassword);
        }
    }
}
