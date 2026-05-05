using Jaina.Messaging.Outbox.EfCore;
using Microsoft.EntityFrameworkCore;

namespace JainaShop.Orders;

public sealed class OrdersDb : DbContext
{
    public OrdersDb(DbContextOptions<OrdersDb> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.Sku).HasMaxLength(50).IsRequired();
            b.Property(o => o.Total).HasPrecision(12, 2);
        });

        // Outbox messages live on the same DbContext — committed atomically with order writes
        modelBuilder.ApplyJainaOutbox();
    }
}
