using Jaina.Data.Cqrs.Events;

namespace Jaina.Data.Cqrs.Models;

public abstract class AggregateRoot
{
    public Guid Id { get; protected set; }
    public long Version { get; protected set; }

    private readonly List<IDomainEvent> _uncommittedEvents = new();

    public IReadOnlyCollection<IDomainEvent> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();
    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    protected void AddEvent(IDomainEvent @event)
    {
        _uncommittedEvents.Add(@event);
        Apply(@event);
        Version++;
    }

    protected abstract void Apply(IDomainEvent @event);
}
