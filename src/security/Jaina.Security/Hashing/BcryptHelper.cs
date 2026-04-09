namespace Jaina.Security.Hashing;

public static class BcryptHelper
{
    public static string Hash(string plainText, int workFactor = 10)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            throw new HashingException("Plain text is null or empty");
        return BCrypt.Net.BCrypt.HashPassword(plainText, workFactor);
    }

    public static bool Verify(string plainText, string hashedText) =>
        BCrypt.Net.BCrypt.Verify(plainText, hashedText);
}
