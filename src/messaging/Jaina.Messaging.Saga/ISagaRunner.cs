namespace Jaina.Messaging.Saga;

/// <summary>
/// Executes a <see cref="Saga{TState}"/> against a state instance. Persists between every
/// step via the registered <see cref="ISagaRepository{TState}"/>. On failure, compensates
/// completed steps in reverse and throws <see cref="SagaFailedException"/>.
/// </summary>
public interface ISagaRunner<TSaga, TState>
    where TSaga : Saga<TState>
    where TState : SagaState
{
    /// <summary>
    /// Run the saga to completion or compensation. Resumes from a partially-completed state
    /// by skipping steps already in <see cref="SagaState.CompletedSteps"/>.
    /// </summary>
    /// <returns>The final state. Inspect <c>IsCompleted</c> / <c>IsCompensated</c>.</returns>
    /// <exception cref="SagaFailedException">A forward step threw; compensation completed.</exception>
    Task<TState> RunAsync(TState state, CancellationToken ct = default);
}
