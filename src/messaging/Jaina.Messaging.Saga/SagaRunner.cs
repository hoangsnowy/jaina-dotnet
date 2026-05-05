using Jaina.Observability.Telemetry;
using Microsoft.Extensions.Logging;

namespace Jaina.Messaging.Saga;

public sealed class SagaRunner<TSaga, TState> : ISagaRunner<TSaga, TState>
    where TSaga : Saga<TState>
    where TState : SagaState
{
    private readonly TSaga _saga;
    private readonly ISagaRepository<TState> _repo;
    private readonly ILogger<SagaRunner<TSaga, TState>> _logger;

    public SagaRunner(TSaga saga, ISagaRepository<TState> repo, ILogger<SagaRunner<TSaga, TState>> logger)
    {
        _saga = saga;
        _repo = repo;
        _logger = logger;
    }

    public async Task<TState> RunAsync(TState state, CancellationToken ct = default)
    {
        using var rootSpan = JainaActivitySource.StartSpan("saga", "run");
        rootSpan?.SetTag(TagConventions.SagaCorrelation, state.CorrelationId.ToString());
        await _repo.SaveAsync(state, ct);

        var completed = new HashSet<string>(state.CompletedSteps, StringComparer.Ordinal);
        ISagaStep<TState>? failingStep = null;
        Exception? failure = null;

        foreach (var step in _saga.Steps)
        {
            if (completed.Contains(step.Name))
            {
                _logger.LogDebug("Saga {Id} skipping completed step {Step}", state.CorrelationId, step.Name);
                continue;
            }

            using var stepSpan = JainaActivitySource.StartSpan("saga", "step");
            stepSpan?.SetTag(TagConventions.SagaCorrelation, state.CorrelationId.ToString());
            stepSpan?.SetTag(TagConventions.SagaStep, step.Name);
            try
            {
                await step.ExecuteAsync(state, ct);
                state.CompletedSteps.Add(step.Name);
                completed.Add(step.Name);
                await _repo.SaveAsync(state, ct);
            }
            catch (Exception ex)
            {
                stepSpan?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                _logger.LogWarning(ex, "Saga {Id} failed at step {Step}", state.CorrelationId, step.Name);
                state.FailedAt = step.Name;
                state.LastError = ex.Message;
                failingStep = step;
                failure = ex;
                break;
            }
        }

        if (failure is null)
        {
            state.IsCompleted = true;
            await _repo.SaveAsync(state, ct);
            return state;
        }

        await CompensateAsync(state, ct);
        throw new SagaFailedException(state, failure);
    }

    private async Task CompensateAsync(TState state, CancellationToken ct)
    {
        var byName = _saga.Steps.ToDictionary(s => s.Name, StringComparer.Ordinal);
        for (var i = state.CompletedSteps.Count - 1; i >= 0; i--)
        {
            var name = state.CompletedSteps[i];
            if (!byName.TryGetValue(name, out var step)) continue;

            try
            {
                await step.CompensateAsync(state, ct);
                state.CompensatedSteps.Add(name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Saga {Id} compensation failed at step {Step}; state may be inconsistent",
                    state.CorrelationId, name);
                // continue compensating remaining — best-effort. Operator must inspect state.
            }
            finally
            {
                await _repo.SaveAsync(state, ct);
            }
        }
        state.IsCompensated = true;
        await _repo.SaveAsync(state, ct);
    }
}
