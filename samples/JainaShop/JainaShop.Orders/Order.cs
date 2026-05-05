namespace JainaShop.Orders;

public sealed class Order
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset PlacedAt { get; init; } = DateTimeOffset.UtcNow;
}

public record OrderPlaced(Guid OrderId, string Sku, int Quantity, decimal Total);
