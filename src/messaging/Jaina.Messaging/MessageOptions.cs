namespace Jaina.Messaging;

public class MessageOptions
{
    public string Label { get; set; } = "";
    public string SessionId { get; set; } = "";
    public int MessageOrder { get; set; } = -1;
    public bool LastMessage { get; set; } = true;
    public IDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();
}
