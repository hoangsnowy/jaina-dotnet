using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Jaina.Notifications.Email;

public class SmtpEmailSenderOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpEmailSenderOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpEmailSenderOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(message.FromDisplayName, message.From));
        message.To.ForEach(to => mime.To.Add(MailboxAddress.Parse(to)));
        message.Cc.ForEach(cc => mime.Cc.Add(MailboxAddress.Parse(cc)));
        message.Bcc.ForEach(bcc => mime.Bcc.Add(MailboxAddress.Parse(bcc)));
        mime.Subject = message.Subject;

        var builder = new BodyBuilder();
        if (message.IsHtml)
            builder.HtmlBody = message.Body;
        else
            builder.TextBody = message.Body;

        foreach (var att in message.Attachments)
            builder.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.ContentType));

        foreach (var img in message.InlineImages)
        {
            var inline = builder.LinkedResources.Add(img.ContentId, img.Content, ContentType.Parse(img.ContentType));
            inline.ContentId = img.ContentId;
        }

        mime.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, _options.UseSsl, ct);
        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Email sent to {Recipients}", string.Join(", ", message.To));
    }
}
