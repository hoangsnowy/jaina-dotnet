using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Jaina.Messaging.Outbox.EfCore;

public static class OutboxModelBuilderExtensions
{
    /// <summary>
    /// Map <see cref="OutboxMessage"/> as a table named <c>jaina_outbox_messages</c>.
    /// Call from your DbContext's <c>OnModelCreating</c> override.
    /// </summary>
    public static ModelBuilder ApplyJainaOutbox(this ModelBuilder modelBuilder, string tableName = "jaina_outbox_messages")
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable(tableName);
            b.HasKey(m => m.Id);

            b.Property(m => m.PayloadType).IsRequired().HasMaxLength(500);
            b.Property(m => m.Payload).IsRequired();
            b.Property(m => m.Destination).HasMaxLength(200);
            b.Property(m => m.CreatedAt).IsRequired();
            b.Property(m => m.ScheduledFor).IsRequired();
            b.Property(m => m.LastError).HasMaxLength(2000);

            // Headers as JSON column with a value comparer so EF tracks changes correctly
            var dictComparer = new ValueComparer<IDictionary<string, string>>(
                (a, c) => ReferenceEquals(a, c) || (a != null && c != null && a.SequenceEqual(c)),
                v => v == null ? 0 : v.Aggregate(0, (acc, kv) => HashCode.Combine(acc, kv.Key, kv.Value)),
                v => v == null ? new Dictionary<string, string>() : new Dictionary<string, string>(v));

            b.Property(m => m.Headers)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new())
                .Metadata.SetValueComparer(dictComparer);

            // Index speeds up the relay's "pending due now" query
            b.HasIndex(m => new { m.ProcessedAt, m.ScheduledFor }).HasDatabaseName("ix_jaina_outbox_pending");
        });

        return modelBuilder;
    }
}
