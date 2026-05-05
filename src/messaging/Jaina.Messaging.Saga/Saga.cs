namespace Jaina.Messaging.Saga;

/// <summary>
/// Define a saga as an ordered list of <see cref="ISagaStep{TState}"/>. Inherit and provide
/// the steps; the runner executes them and compensates on failure.
/// </summary>
public abstract class Saga<TState> where TState : SagaState
{
    public abstract IReadOnlyList<ISagaStep<TState>> Steps { get; }
}
