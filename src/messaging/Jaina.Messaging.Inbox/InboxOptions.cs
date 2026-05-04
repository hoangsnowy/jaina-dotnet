namespace Jaina.Messaging.Inbox;

/// <summary>
/// Configuration for the inbox dedup store.
/// </summary>
public sealed class InboxOptions
{
    /// <summary>
    /// How long to retain a dedup record. Should be longer than any reasonable consumer
    /// retry window, but shorter than ∞ so the store doesn't grow forever. Default 7 days.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromDays(7);
}
