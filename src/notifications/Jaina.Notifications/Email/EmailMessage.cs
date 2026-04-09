namespace Jaina.Notifications.Email;

public class EmailMessage
{
    public string From { get; set; } = "";
    public string FromDisplayName { get; set; } = "";
    public List<string> To { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public List<string> Bcc { get; set; } = new();
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsHtml { get; set; } = true;
    public List<EmailAttachment> Attachments { get; set; } = new();
    public List<EmailInlineImage> InlineImages { get; set; } = new();
}

public class EmailAttachment
{
    public string FileName { get; set; } = "";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
}

public class EmailInlineImage
{
    public string ContentId { get; set; } = "";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "image/png";
}
