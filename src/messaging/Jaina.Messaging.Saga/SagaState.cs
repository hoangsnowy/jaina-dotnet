namespace Jaina.Messaging.Saga;

/// <summary>
/// Base type for saga state. Domain-specific state inherits and adds payload fields.
/// The runner persists this between steps so a crashed saga can resume.
/// </summary>
public abstract class SagaState
{
    /// <summary>Stable correlation id for this saga instance. Set on creation.</summary>
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    /// <summary>Names of steps that completed successfully, in order.</summary>
    public List<string> CompletedSteps { get; init; } = new();

    /// <summary>Names of steps that ran their compensation, in compensation order.</summary>
    public List<string> CompensatedSteps { get; init; } = new();

    /// <summary>Name of the step that threw (drives the compensation walk). Null on success.</summary>
    public string? FailedAt { get; set; }

    /// <summary>Last exception message (for diagnostics). Null on success.</summary>
    public string? LastError { get; set; }

    /// <summary>True once all steps completed without failure.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>True once compensations finished after a failure (terminal failure state).</summary>
    public bool IsCompensated { get; set; }
}
