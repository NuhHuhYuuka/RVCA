using SecurityData.Models;
using System;
using System.Security.Cryptography;
using System.Text;

namespace SecurityData.Services
{
    public static class SecurityService
    {
        private const int NonceSize = 12; // recommended for GCM
        private const int TagSize = 16;   // 128-bit auth tag

        public static EncryptionResult Encrypt(string plainText, byte[] sessionKey)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                throw new ArgumentException("plainText không được rỗng.");

            if (sessionKey == null || sessionKey.Length < 16)
                throw new ArgumentException("sessionKey không hợp lệ.");

            byte[] nonce = new byte[NonceSize];
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = new byte[plaintextBytes.Length];
            byte[] tag = new byte[TagSize];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            using (var aesGcm = new AesGcm(sessionKey))
            {
                aesGcm.Encrypt(nonce, plaintextBytes, cipherBytes, tag);
            }

            return new EncryptionResult
            {
                CipherText = Convert.ToBase64String(cipherBytes),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag)
            };
        }

        public static string Decrypt(string cipherTextBase64, string nonceBase64, string tagBase64, byte[] sessionKey)
        {
            if (string.IsNullOrWhiteSpace(cipherTextBase64))
                throw new ArgumentException("cipherText không được rỗng.");

            if (string.IsNullOrWhiteSpace(nonceBase64))
                throw new ArgumentException("nonce không được rỗng.");

            if (string.IsNullOrWhiteSpace(tagBase64))
                throw new ArgumentException("tag không được rỗng.");

            if (sessionKey == null || sessionKey.Length < 16)
                throw new ArgumentException("sessionKey không hợp lệ.");

            try
            {
                byte[] nonce = Convert.FromBase64String(nonceBase64);
                byte[] tag = Convert.FromBase64String(tagBase64);
                byte[] cipherBytes = Convert.FromBase64String(cipherTextBase64);
                byte[] plaintextBytes = new byte[cipherBytes.Length];

                using (var aesGcm = new AesGcm(sessionKey))
                {
                    aesGcm.Decrypt(nonce, cipherBytes, tag, plaintextBytes);
                }

                return Encoding.UTF8.GetString(plaintextBytes);
            }
            catch
            {
                return "[Lỗi giải mã / sai session key / dữ liệu bị sửa đổi]";
            }
        }

        public static EncryptionResult EncryptBytes(byte[] data, byte[] sessionKey)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("data không hợp lệ.");

            if (sessionKey == null || sessionKey.Length < 16)
                throw new ArgumentException("sessionKey không hợp lệ.");

            byte[] nonce = new byte[NonceSize];
            byte[] cipherBytes = new byte[data.Length];
            byte[] tag = new byte[TagSize];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            using (var aesGcm = new AesGcm(sessionKey))
            {
                aesGcm.Encrypt(nonce, data, cipherBytes, tag);
            }

            return new EncryptionResult
            {
                CipherText = Convert.ToBase64String(cipherBytes),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag)
            };
        }

        public static byte[] DecryptBytes(string cipherTextBase64, string nonceBase64, string tagBase64, byte[] sessionKey)
        {
            byte[] nonce = Convert.FromBase64String(nonceBase64);
            byte[] tag = Convert.FromBase64String(tagBase64);
            byte[] cipherBytes = Convert.FromBase64String(cipherTextBase64);
            byte[] plainBytes = new byte[cipherBytes.Length];

            using (var aesGcm = new AesGcm(sessionKey))
            {
                aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }

            return plainBytes;
        }
    }
}