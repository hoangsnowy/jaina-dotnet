namespace Jaina.Messaging.Saga;

/// <summary>
/// Thrown by <see cref="ISagaRunner{TSaga,TState}"/> after a forward step throws and
/// compensation completes. Carries the final state so callers can inspect which steps ran
/// and which compensations succeeded.
/// </summary>
public sealed class SagaFailedException : Exception
{
    public SagaFailedException(SagaState state, Exception inner)
        : base($"Saga {state.CorrelationId} failed at step '{state.FailedAt}': {inner.Message}", inner)
    {
        State = state;
    }

    public SagaState State { get; }
}
