namespace Jaina.Messaging.Saga;

/// <summary>
/// One step in a saga. <see cref="ExecuteAsync"/> performs the forward action;
/// <see cref="CompensateAsync"/> undoes it if a later step fails.
/// </summary>
public interface ISagaStep<TState> where TState : SagaState
{
    /// <summary>Stable name (used for resume + history). Convention: PascalCase verb-noun.</summary>
    string Name { get; }

    /// <summary>Execute the forward action. Throw to trigger compensation.</summary>
    Task ExecuteAsync(TState state, CancellationToken ct);

    /// <summary>
    /// Undo the forward action. Called in reverse order for steps that previously completed
    /// when a later step throws. Implementations should be idempotent — compensation may be
    /// retried if the runner crashes mid-walk.
    /// </summary>
    Task CompensateAsync(TState state, CancellationToken ct);
}
