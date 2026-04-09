namespace Jaina.Data.Cqrs.Events;

public interface IEventStore
{
    Task SaveEventAsync(IDomainEvent @event, CancellationToken ct = default);
    Task<IEnumerable<EventData>> GetEventsAsync(Guid aggregateId, CancellationToken ct = default);
}

public class EventData
{
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = "";
    public string Data { get; set; } = "";
    public DateTime OccurredOn { get; set; }
    public long Version { get; set; }
}
