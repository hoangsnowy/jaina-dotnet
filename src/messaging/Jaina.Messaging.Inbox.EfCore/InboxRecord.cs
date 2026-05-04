namespace Jaina.Messaging.Inbox.EfCore;

/// <summary>
/// Persisted dedup record. Composite key (Consumer, MessageId) — the EF unique constraint
/// is what makes <see cref="EfInboxStore{TDbContext}.TryConsumeAsync"/> atomic: two
/// concurrent consumers calling <c>SaveChanges</c> with the same key cause exactly one to
/// throw <c>DbUpdateException</c>, which the store turns into a <c>false</c> return.
/// </summary>
public sealed class InboxRecord
{
    public string Consumer { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; }
}
