using System.Collections.Concurrent;

namespace Jaina.Messaging.Saga.InMemory;

/// <summary>
/// Process-local <see cref="ISagaRepository{TState}"/>. Loses state on restart and is not
/// safe across multiple processes — use the EF Core or Redis provider in production.
/// </summary>
public sealed class InMemorySagaRepository<TState> : ISagaRepository<TState>
    where TState : SagaState
{
    private readonly ConcurrentDictionary<Guid, TState> _state = new();

    public Task<TState?> LoadAsync(Guid correlationId, CancellationToken ct = default) =>
        Task.FromResult(_state.TryGetValue(correlationId, out var s) ? s : null);

    public Task SaveAsync(TState state, CancellationToken ct = default)
    {
        _state[state.CorrelationId] = state;
        return Task.CompletedTask;
    }

    /// <summary>Test helper — snapshot of all stored states.</summary>
    public IReadOnlyCollection<TState> Snapshot() => _state.Values.ToArray();
}
