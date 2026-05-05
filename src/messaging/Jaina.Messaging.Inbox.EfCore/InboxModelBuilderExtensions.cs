using Microsoft.EntityFrameworkCore;

namespace Jaina.Messaging.Inbox.EfCore;

public static class InboxModelBuilderExtensions
{
    /// <summary>
    /// Map <see cref="InboxRecord"/> as <c>jaina_inbox_records</c> with a composite primary
    /// key on (Consumer, MessageId). Call from your DbContext's <c>OnModelCreating</c>.
    /// </summary>
    public static ModelBuilder ApplyJainaInbox(this ModelBuilder modelBuilder, string tableName = "jaina_inbox_records")
    {
        modelBuilder.Entity<InboxRecord>(b =>
        {
            b.ToTable(tableName);
            b.HasKey(r => new { r.Consumer, r.MessageId });
            b.Property(r => r.Consumer).HasMaxLength(200).IsRequired();
            b.Property(r => r.MessageId).HasMaxLength(200).IsRequired();
            b.Property(r => r.CreatedAt).IsRequired();
            b.Property(r => r.ExpiresAt).IsRequired();
            b.HasIndex(r => r.ExpiresAt).HasDatabaseName("ix_jaina_inbox_expiry");
        });
        return modelBuilder;
    }
}
