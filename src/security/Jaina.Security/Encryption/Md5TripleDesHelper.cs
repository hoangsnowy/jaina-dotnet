using System.Security.Cryptography;
using System.Text;

namespace Jaina.Security.Encryption;

public static class Md5TripleDesHelper
{
    public static string Encrypt(string key, string text)
    {
        using var md5 = MD5.Create();
        using var tdes = TripleDES.Create();
        tdes.Key = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
        tdes.Mode = CipherMode.ECB;
        tdes.Padding = PaddingMode.PKCS7;

        using var transform = tdes.CreateEncryptor();
        var textBytes = Encoding.UTF8.GetBytes(text);
        var bytes = transform.TransformFinalBlock(textBytes, 0, textBytes.Length);
        return Convert.ToBase64String(bytes);
    }

    public static string Decrypt(string key, string cipher)
    {
        using var md5 = MD5.Create();
        using var tdes = TripleDES.Create();
        tdes.Key = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
        tdes.Mode = CipherMode.ECB;
        tdes.Padding = PaddingMode.PKCS7;

        using var transform = tdes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipher);
        var bytes = transform.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(bytes);
    }
}
