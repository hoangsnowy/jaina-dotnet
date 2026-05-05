using Jaina.Messaging.Saga;
using Jaina.Messaging.Saga.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jaina.Messaging.Saga.UnitTests;

public class OrderSagaTests
{
    [Fact]
    public async Task RunAsync_AllStepsSucceed_StateMarkedCompleted()
    {
        // Arrange
        var (runner, repo, calls) = Build();
        var state = new OrderSagaState { Sku = "WIDGET" };

        // Act
        await runner.RunAsync(state);

        // Assert
        Assert.True(state.IsCompleted);
        Assert.False(state.IsCompensated);
        Assert.Equal(new[] { "Reserve", "Charge", "Ship" }, state.CompletedSteps);
        Assert.Empty(state.CompensatedSteps);
        Assert.Equal(new[] { "Reserve", "Charge", "Ship" }, calls);
    }

    [Fact]
    public async Task RunAsync_StepFails_PreviousStepsCompensateInReverse()
    {
        // Arrange — Ship throws
        var (runner, repo, calls) = Build(failAt: "Ship");
        var state = new OrderSagaState { Sku = "WIDGET" };

        // Act
        var ex = await Assert.ThrowsAsync<SagaFailedException>(() => runner.RunAsync(state));

        // Assert — Reserve and Charge ran forward, then compensated in reverse order
        Assert.Equal("Ship", ex.State.FailedAt);
        Assert.False(state.IsCompleted);
        Assert.True(state.IsCompensated);
        Assert.Equal(new[] { "Reserve", "Charge" }, state.CompletedSteps);
        Assert.Equal(new[] { "Charge", "Reserve" }, state.CompensatedSteps);
        // call sequence: forward Reserve, Charge, Ship(throw); compensate Charge, Reserve
        Assert.Equal(new[] { "Reserve", "Charge", "Ship", "Compensate:Charge", "Compensate:Reserve" }, calls);
    }

    [Fact]
    public async Task RunAsync_FirstStepFails_NoCompensationNeeded()
    {
        // Arrange — Reserve throws, no prior steps to compensate
        var (runner, repo, calls) = Build(failAt: "Reserve");
        var state = new OrderSagaState { Sku = "WIDGET" };

        // Act
        await Assert.ThrowsAsync<SagaFailedException>(() => runner.RunAsync(state));

        // Assert
        Assert.Empty(state.CompletedSteps);
        Assert.Empty(state.CompensatedSteps);
        Assert.True(state.IsCompensated);  // compensation walk completed (zero items)
    }

    [Fact]
    public async Task RunAsync_ResumesFromPartialState_SkipsAlreadyCompletedSteps()
    {
        // Arrange — saga that previously got past Reserve and Charge, then crashed before Ship
        var (runner, repo, calls) = Build();
        var state = new OrderSagaState { Sku = "WIDGET" };
        state.CompletedSteps.Add("Reserve");
        state.CompletedSteps.Add("Charge");

        // Act — resume
        await runner.RunAsync(state);

        // Assert — only Ship ran this time
        Assert.True(state.IsCompleted);
        Assert.Equal(new[] { "Ship" }, calls);
    }

    [Fact]
    public async Task RunAsync_PersistsStateToRepository_AfterEveryStep()
    {
        // Arrange
        var (runner, repo, _) = Build();
        var state = new OrderSagaState { Sku = "WIDGET" };

        // Act
        await runner.RunAsync(state);

        // Assert — repo has the final state
        var loaded = await repo.LoadAsync(state.CorrelationId);
        Assert.NotNull(loaded);
        Assert.True(loaded!.IsCompleted);
        Assert.Equal(3, loaded.CompletedSteps.Count);
    }

    // ── Saga + state + steps under test ────────────────────────────────

    private static (SagaRunner<OrderSaga, OrderSagaState> runner,
                    InMemorySagaRepository<OrderSagaState> repo,
                    List<string> calls) Build(string? failAt = null)
    {
        var calls = new List<string>();
        var saga = new OrderSaga(failAt, calls);
        var repo = new InMemorySagaRepository<OrderSagaState>();
        var runner = new SagaRunner<OrderSaga, OrderSagaState>(
            saga, repo, NullLogger<SagaRunner<OrderSaga, OrderSagaState>>.Instance);
        return (runner, repo, calls);
    }

    public sealed class OrderSagaState : SagaState
    {
        public string Sku { get; init; } = "";
    }

    public sealed class OrderSaga : Saga<OrderSagaState>
    {
        public OrderSaga(string? failAt, List<string> calls)
        {
            Steps = new ISagaStep<OrderSagaState>[]
            {
                new TrackingStep("Reserve", failAt == "Reserve", calls),
                new TrackingStep("Charge",  failAt == "Charge",  calls),
                new TrackingStep("Ship",    failAt == "Ship",    calls),
            };
        }

        public override IReadOnlyList<ISagaStep<OrderSagaState>> Steps { get; }
    }

    private sealed class TrackingStep : ISagaStep<OrderSagaState>
    {
        private readonly bool _shouldThrow;
        private readonly List<string> _calls;
        public string Name { get; }

        public TrackingStep(string name, bool shouldThrow, List<string> calls)
        {
            Name = name;
            _shouldThrow = shouldThrow;
            _calls = calls;
        }

        public Task ExecuteAsync(OrderSagaState state, CancellationToken ct)
        {
            _calls.Add(Name);
            if (_shouldThrow) throw new InvalidOperationException($"step {Name} failed");
            return Task.CompletedTask;
        }

        public Task CompensateAsync(OrderSagaState state, CancellationToken ct)
        {
            _calls.Add($"Compensate:{Name}");
            return Task.CompletedTask;
        }
    }
}
