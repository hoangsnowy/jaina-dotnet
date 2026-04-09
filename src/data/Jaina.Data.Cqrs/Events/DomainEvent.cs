namespace Jaina.Data.Cqrs.Events;

public abstract record DomainEvent : IDomainEvent
{
    public Guid AggregateId { get; init; }
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
}
