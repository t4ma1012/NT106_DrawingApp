// ============================================================
// SharedLib/Security/AesHelper.cs
// Tuần 4 — Mã hóa/giải mã AES-256 CBC cho UDP payload
// IV random 16 bytes mỗi lần gửi, đính kèm đầu packet
// ============================================================
using System;
using System.IO;
using System.Security.Cryptography;

namespace SharedLib.Security
{
    public static class AesHelper
    {
        /// <summary>
        /// Mã hóa data: kết quả = [IV(16B)] + [EncryptedData]
        /// </summary>
        public static byte[] Encrypt(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            using var aes = Aes.Create();
            aes.Key = SecurityConfig.AesKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();  // KHÔNG dùng IV cố định

            using var encryptor = aes.CreateEncryptor();
            byte[] encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

            // Ghép [IV(16)] + [CipherText]
            byte[] result = new byte[16 + encrypted.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, 16);
            Buffer.BlockCopy(encrypted, 0, result, 16, encrypted.Length);
            return result;
        }

        /// <summary>
        /// Giải mã data: đọc IV từ 16 bytes đầu, giải mã phần còn lại.
        /// </summary>
        public static byte[] Decrypt(byte[] data)
        {
            if (data == null || data.Length <= 16)
                throw new ArgumentException("Dữ liệu mã hóa quá ngắn (thiếu IV).");

            using var aes = Aes.Create();
            aes.Key = SecurityConfig.AesKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            byte[] iv = new byte[16];
            Buffer.BlockCopy(data, 0, iv, 0, 16);
            aes.IV = iv;

            byte[] cipherText = new byte[data.Length - 16];
            Buffer.BlockCopy(data, 16, cipherText, 0, cipherText.Length);

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
        }

        /// <summary>Test nhanh encrypt → decrypt round-trip (dùng khi debug).</summary>
        public static bool TestRoundTrip(string testMessage = "NT106_TEST_AES_256")
        {
            try
            {
                byte[] original = System.Text.Encoding.UTF8.GetBytes(testMessage);
                byte[] encrypted = Encrypt(original);
                byte[] decrypted = Decrypt(encrypted);
                string result = System.Text.Encoding.UTF8.GetString(decrypted);
                return result == testMessage;
            }
            catch
            {
                return false;
            }
        }
    }
}
