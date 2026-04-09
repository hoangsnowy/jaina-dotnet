namespace Jaina.Security.Encryption;

public class EncryptException : Exception
{
    public EncryptException(string message) : base(message) { }
    public EncryptException(string message, Exception innerException) : base(message, innerException) { }
}
