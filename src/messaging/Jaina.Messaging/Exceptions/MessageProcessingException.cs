namespace Jaina.Messaging.Exceptions;

public class MessageProcessingException : Exception
{
    public MessageProcessingException(string message) : base(message) { }
    public MessageProcessingException(string message, Exception innerException) : base(message, innerException) { }
}
