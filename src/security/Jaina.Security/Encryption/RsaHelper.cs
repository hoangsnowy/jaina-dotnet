using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Jaina.Security.Encryption;

public static class RsaHelper
{
    public static string Encrypt(string plainText, string publicKeyXml)
    {
        if (string.IsNullOrWhiteSpace(plainText)) throw new EncryptException("Plain text is null or empty");
        if (string.IsNullOrWhiteSpace(publicKeyXml)) throw new EncryptException("Public key is null or empty");

        using var rsa = RSA.Create();
        rsa.FromXmlString(publicKeyXml);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = rsa.Encrypt(plainBytes, RSAEncryptionPadding.OaepSHA1);
        return Convert.ToBase64String(cipherBytes);
    }

    public static string Decrypt(string cipherText, string privateKeyXml)
    {
        if (string.IsNullOrWhiteSpace(cipherText)) throw new EncryptException("Cipher text is null or empty");
        if (string.IsNullOrWhiteSpace(privateKeyXml)) throw new EncryptException("Private key is null or empty");

        using var rsa = RSA.Create();
        rsa.FromXmlString(privateKeyXml);
        var cipherBytes = Convert.FromBase64String(cipherText);
        var plainBytes = rsa.Decrypt(cipherBytes, RSAEncryptionPadding.OaepSHA1);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public static (string PrivateKeyXml, string PublicKeyXml) GenerateKeys(int keySizeInBits = 2048)
    {
#if NET5_0_OR_GREATER
        using var rsa = RSA.Create(keySizeInBits);
#else
        using var rsa = RSA.Create();
        rsa.KeySize = keySizeInBits;
#endif
        return (rsa.ToXmlString(includePrivateParameters: true), rsa.ToXmlString(includePrivateParameters: false));
    }
}
