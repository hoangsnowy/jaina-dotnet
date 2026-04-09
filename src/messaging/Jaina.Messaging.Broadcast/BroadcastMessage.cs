namespace Jaina.Messaging.Broadcast;

public class BroadcastMessage<T>
{
    public string Channel { get; set; } = "";
    public T? Payload { get; set; }
    public IDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();
}
