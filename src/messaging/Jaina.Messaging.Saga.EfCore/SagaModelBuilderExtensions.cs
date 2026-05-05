using Microsoft.EntityFrameworkCore;

namespace Jaina.Messaging.Saga.EfCore;

public static class SagaModelBuilderExtensions
{
    /// <summary>
    /// Map <see cref="SagaStateRecord"/> as <c>jaina_saga_states</c>. Call from your
    /// DbContext's <c>OnModelCreating</c>.
    /// </summary>
    public static ModelBuilder ApplyJainaSaga(this ModelBuilder modelBuilder, string tableName = "jaina_saga_states")
    {
        modelBuilder.Entity<SagaStateRecord>(b =>
        {
            b.ToTable(tableName);
            b.HasKey(s => s.CorrelationId);
            b.Property(s => s.StateType).HasMaxLength(500).IsRequired();
            b.Property(s => s.Payload).IsRequired();
            b.Property(s => s.UpdatedAt).IsRequired();
        });
        return modelBuilder;
    }
}
