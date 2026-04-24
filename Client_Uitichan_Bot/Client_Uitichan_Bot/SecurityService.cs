using System;
using System.IO;
using System.Security.Cryptography;

namespace Client_Uitichan_Bot
{
    public class SecurityService
    {
        // Mã hóa AES với IV ngẫu nhiên và salt cho mỗi tin nhắn
        public static string Encrypt(string plainText, string passphrase)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;

                byte[] salt = new byte[16];
                byte[] iv = new byte[16];

                // Sử dụng RandomNumberGenerator thay cho RNGCryptoServiceProvider đã cũ
                RandomNumberGenerator.Fill(salt);
                RandomNumberGenerator.Fill(iv);

                var key = new Rfc2898DeriveBytes(passphrase, salt, 100000, HashAlgorithmName.SHA256);

                aes.Key = key.GetBytes(32);
                aes.IV = iv;

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (var memoryStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (var streamWriter = new StreamWriter(cryptoStream))
                        {
                            streamWriter.Write(plainText);
                        }
                    }

                    byte[] cipherText = memoryStream.ToArray();
                    byte[] resultData = new byte[salt.Length + iv.Length + cipherText.Length];

                    Buffer.BlockCopy(salt, 0, resultData, 0, salt.Length);
                    Buffer.BlockCopy(iv, 0, resultData, salt.Length, iv.Length);
                    Buffer.BlockCopy(cipherText, 0, resultData, salt.Length + iv.Length, cipherText.Length);

                    return Convert.ToBase64String(resultData);
                }
            }
        }

        // Giải mã AES với IV và Salt được gửi kèm
        public static string Decrypt(string cipherText, string passphrase)
        {
            try
            {
                byte[] fullCipherData = Convert.FromBase64String(cipherText);

                byte[] salt = new byte[16];
                byte[] iv = new byte[16];
                Buffer.BlockCopy(fullCipherData, 0, salt, 0, salt.Length);
                Buffer.BlockCopy(fullCipherData, salt.Length, iv, 0, iv.Length);

                byte[] cipherBytes = new byte[fullCipherData.Length - salt.Length - iv.Length];
                Buffer.BlockCopy(fullCipherData, salt.Length + iv.Length, cipherBytes, 0, cipherBytes.Length);

                var key = new Rfc2898DeriveBytes(passphrase, salt, 100000, HashAlgorithmName.SHA256);
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Key = key.GetBytes(32);
                    aes.IV = iv;

                    var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (var memoryStream = new MemoryStream(cipherBytes))
                    {
                        using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                        {
                            using (var streamReader = new StreamReader(cryptoStream))
                            {
                                return streamReader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch
            {
                return "[Lỗi giải mã hoặc tin nhắn không hợp lệ]";
            }
        }
    }
}