namespace Jaina.Messaging.Saga.EfCore;

/// <summary>
/// EF Core entity that stores a saga's serialized state as JSON. Saga implementations
/// don't subclass this — the runner serializes the user's <c>SagaState</c> subclass and
/// stores it via <see cref="EfSagaRepository{TState,TDbContext}"/>.
/// </summary>
public sealed class SagaStateRecord
{
    public Guid CorrelationId { get; init; }
    public string StateType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
