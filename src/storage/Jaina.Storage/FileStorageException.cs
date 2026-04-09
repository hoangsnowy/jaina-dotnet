namespace Jaina.Storage;

public class FileStorageException : Exception
{
    public FileStorageException(string message) : base(message) { }
    public FileStorageException(string message, Exception innerException) : base(message, innerException) { }
}
