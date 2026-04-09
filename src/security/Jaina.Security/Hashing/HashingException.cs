namespace Jaina.Security.Hashing;

public class HashingException : Exception
{
    public HashingException(string message) : base(message) { }
    public HashingException(string message, Exception innerException) : base(message, innerException) { }
}
