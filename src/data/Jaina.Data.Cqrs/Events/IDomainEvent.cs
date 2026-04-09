namespace Jaina.Data.Cqrs.Events;

public interface IDomainEvent
{
    Guid AggregateId { get; }
    DateTime OccurredOn { get; }
    string EventType { get; }
}

public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
