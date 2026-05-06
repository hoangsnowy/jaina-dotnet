using Jaina.AspNetCore;
using Jaina.BackgroundJobs;
using Jaina.HealthChecks;
using Jaina.Messaging.Inbox;
using Jaina.Messaging.Inbox.InMemory;
using Jaina.Notifications.ConsoleSms;
using Jaina.Notifications.Sms;
using Jaina.Samples.ServiceDefaults;
using JainaShop.Notifier;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddJainaProblemDetails();

builder.Services.AddJainaConsoleSms();
builder.Services.AddJainaInMemoryInbox();
builder.Services.AddTransient<IBackgroundJob<OrderPlacedPayload>, NotifyOrderPlacedJob>();

builder.Services.AddHealthChecks()
    .AddCheck("notifier-ready", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
        tags: new[] { JainaHealthCheckTags.Ready });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseJainaPipeline();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapJainaHealthChecks();

// POST /events/order-placed — receive an OrderPlaced event from a broker (or direct test
// call). The Inbox dedups by message-id; the BackgroundJobs scheduler queues the SMS
// dispatch so the request returns fast.
app.MapPost("/events/order-placed", async (
    OrderPlacedPayload payload,
    IInboxStore inbox,
    IBackgroundJob<OrderPlacedPayload> job,
    CancellationToken ct) =>
{
    var firstSeen = await inbox.TryConsumeAsync("notifier", payload.OrderId.ToString(), TimeSpan.FromDays(7));
    if (!firstSeen) return Results.Accepted(value: new { duplicate = true });

    await job.ExecuteAsync(payload, ct);

    return Results.Accepted(value: new { processed = true });
});

app.Run();

namespace JainaShop.Notifier
{
    public sealed record OrderPlacedPayload(Guid OrderId, string Sku, int Quantity, string CustomerPhone);

    public sealed class NotifyOrderPlacedJob : IBackgroundJob<OrderPlacedPayload>
    {
        private readonly ISmsSender _sms;
        private readonly ILogger<NotifyOrderPlacedJob> _logger;

        public NotifyOrderPlacedJob(ISmsSender sms, ILogger<NotifyOrderPlacedJob> logger)
        {
            _sms = sms;
            _logger = logger;
        }

        public async Task ExecuteAsync(OrderPlacedPayload payload, CancellationToken ct)
        {
            _logger.LogInformation("Sending SMS for order {OrderId}", payload.OrderId);
            await _sms.SendAsync(new SmsMessage
            {
                From = "JainaShop",
                To = payload.CustomerPhone,
                Body = $"Order {payload.OrderId} confirmed: {payload.Quantity} x {payload.Sku}",
            });
        }
    }
}
