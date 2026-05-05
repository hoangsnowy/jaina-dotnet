namespace Jaina.Messaging.Saga;

/// <summary>
/// Persistence for saga state — load by correlation id and save after every step.
/// Providers implement this against EF Core, Redis, etc. The InMemory provider is for tests.
/// </summary>
public interface ISagaRepository<TState> where TState : SagaState
{
    /// <summary>Load saga state by correlation id; null if no saga has been started.</summary>
    Task<TState?> LoadAsync(Guid correlationId, CancellationToken ct = default);

    /// <summary>Save current state. Called after every step (forward and compensation).</summary>
    Task SaveAsync(TState state, CancellationToken ct = default);
}
