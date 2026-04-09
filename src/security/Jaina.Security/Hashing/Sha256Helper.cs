using System.Security.Cryptography;
using System.Text;

namespace Jaina.Security.Hashing;

public static class Sha256Helper
{
    public static string Hash(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            throw new HashingException("Plain text is null or empty");
#if NET5_0_OR_GREATER
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainText));
#else
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(plainText));
#endif
        return Convert.ToBase64String(hashBytes);
    }
}
