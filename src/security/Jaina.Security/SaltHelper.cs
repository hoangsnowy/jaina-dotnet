using System.Security.Cryptography;

namespace Jaina.Security;

public static class SaltHelper
{
    public static string GenerateSalt(int maxLength)
    {
        var salt = new byte[maxLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return Convert.ToBase64String(salt);
    }
}
