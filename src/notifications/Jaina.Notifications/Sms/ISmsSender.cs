namespace Jaina.Notifications.Sms;

public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken ct = default);
}

public class SmsMessage
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Body { get; set; } = "";
}
