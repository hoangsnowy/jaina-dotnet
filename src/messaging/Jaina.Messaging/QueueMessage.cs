namespace Jaina.Messaging;

public class QueueMessage<T>
{
    public T Message { get; }
    public string Label { get; set; } = "";
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public int MessageOrder { get; set; } = -1;
    public bool LastMessage { get; set; } = true;
    public IDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();

    public QueueMessage(T message)
    {
        Message = message;
    }
}
