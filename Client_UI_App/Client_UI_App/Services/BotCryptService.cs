using System;
using System.IO;
using System.Security.Cryptography;

namespace Client_UI_App.Services
{
    // Thuật toán PHẢI KHỚP HOÀN TOÀN với Client_Uitichan_Bot.SecurityService
    // AES-256-CBC + PBKDF2-SHA256 (100.000 iterations)
    // Định dạng wire: [16-byte salt][16-byte IV][ciphertext] -> Base64 string
    internal static class BotCryptService
    {
        public static string Encrypt(string plainText, string passphrase)
        {
            using Aes aes = Aes.Create();
            aes.KeySize   = 256;
            aes.BlockSize = 128;

            byte[] salt = new byte[16];
            byte[] iv   = new byte[16];
            RandomNumberGenerator.Fill(salt);
            RandomNumberGenerator.Fill(iv);

            // Dùng Rfc2898DeriveBytes.Pbkdf2 (API mới .NET 10) – kết quả GIỐNG HỆT Bot
            aes.Key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, 100_000, HashAlgorithmName.SHA256, 32);
            aes.IV  = iv;

            using MemoryStream ms = new();
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
                sw.Write(plainText);

            byte[] cipher = ms.ToArray();
            byte[] result = new byte[salt.Length + iv.Length + cipher.Length];
            Buffer.BlockCopy(salt,   0, result, 0,                   salt.Length);
            Buffer.BlockCopy(iv,     0, result, salt.Length,         iv.Length);
            Buffer.BlockCopy(cipher, 0, result, salt.Length + iv.Length, cipher.Length);

            return Convert.ToBase64String(result);
        }

        public static string Decrypt(string cipherTextBase64, string passphrase)
        {
            try
            {
                byte[] full = Convert.FromBase64String(cipherTextBase64);

                byte[] salt = new byte[16];
                byte[] iv   = new byte[16];
                Buffer.BlockCopy(full, 0,           salt, 0, 16);
                Buffer.BlockCopy(full, 16,          iv,   0, 16);

                byte[] cipher = new byte[full.Length - 32];
                Buffer.BlockCopy(full, 32, cipher, 0, cipher.Length);

                using Aes aes = Aes.Create();
                aes.Key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, 100_000, HashAlgorithmName.SHA256, 32);
                aes.IV  = iv;

                using MemoryStream ms = new(cipher);
                using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                return sr.ReadToEnd();
            }
            catch
            {
                return "[Lỗi giải mã – sai khóa hoặc dữ liệu bị hỏng]";
            }
        }
    }
}
