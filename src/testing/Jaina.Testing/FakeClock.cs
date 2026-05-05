namespace Jaina.Testing;

/// <summary>
/// Deterministic <see cref="TimeProvider"/> for tests. Set <see cref="Now"/> directly or
/// call <see cref="Advance"/> to move time forward. Pass to system-under-test that takes
/// a <see cref="TimeProvider"/> dependency.
/// </summary>
public sealed class FakeClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan delta) => Now += delta;
}
