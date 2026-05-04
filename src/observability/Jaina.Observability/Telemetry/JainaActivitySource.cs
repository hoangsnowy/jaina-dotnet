using System.Diagnostics;

namespace Jaina.Observability.Telemetry;

/// <summary>
/// Single shared <see cref="ActivitySource"/> all Jaina providers emit spans through.
/// OTEL exporters subscribe to this source name to capture every Jaina operation:
/// <code>
/// otel.AddSource(JainaActivitySource.Name);
/// </code>
/// Span name convention: <c>jaina.&lt;module&gt;.&lt;operation&gt;</c> — e.g.
/// <c>jaina.cache.get</c>, <c>jaina.outbox.dispatch</c>, <c>jaina.saga.run</c>.
/// </summary>
public static class JainaActivitySource
{
    /// <summary>Source name registered with OTEL. Stable across versions.</summary>
    public const string Name = "Jaina";

    /// <summary>The shared <see cref="ActivitySource"/> instance.</summary>
    public static readonly ActivitySource Instance = new(Name);

    /// <summary>
    /// Start a span with the standard naming convention. Returns null when no listener is
    /// subscribed (lightweight no-op path).
    /// </summary>
    public static Activity? StartSpan(string module, string operation, ActivityKind kind = ActivityKind.Internal) =>
        Instance.StartActivity($"jaina.{module}.{operation}", kind);
}
