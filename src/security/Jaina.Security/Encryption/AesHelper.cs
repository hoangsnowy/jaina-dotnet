using System.Security.Cryptography;
using System.Text;

namespace Jaina.Security.Encryption;

public static class AesHelper
{
    private static Aes CreateAes(string pepper, string salt)
    {
        var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.CBC;
#if NET5_0_OR_GREATER
        aes.Key = SHA256.HashData(Encoding.ASCII.GetBytes(pepper + salt));
#else
        using var sha256 = SHA256.Create();
        aes.Key = sha256.ComputeHash(Encoding.ASCII.GetBytes(pepper + salt));
#endif
        aes.GenerateIV();
        return aes;
    }

    public static string Encrypt(string plainText, string pepper, string salt)
    {
        if (string.IsNullOrWhiteSpace(plainText)) throw new EncryptException("Plain text is null or empty");
        if (string.IsNullOrWhiteSpace(salt)) throw new EncryptException("Salt is null or empty");

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        using var aes = CreateAes(pepper, salt);
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(plainBytes, 0, plainBytes.Length);
            cs.FlushFinalBlock();
        }

        var encrypted = ms.ToArray();
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string cipherText, string pepper, string salt)
    {
        if (string.IsNullOrWhiteSpace(cipherText)) throw new EncryptException("Cipher text is null or empty");
        if (string.IsNullOrWhiteSpace(salt)) throw new EncryptException("Salt is null or empty");

        var cipherBytes = Convert.FromBase64String(cipherText);
        using var aes = CreateAes(pepper, salt);

        var iv = new byte[aes.IV.Length];
        var actualCipher = new byte[cipherBytes.Length - aes.IV.Length];
        Buffer.BlockCopy(cipherBytes, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, iv.Length, actualCipher, 0, actualCipher.Length);
        aes.IV = iv;

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
        {
            cs.Write(actualCipher, 0, actualCipher.Length);
            cs.FlushFinalBlock();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
