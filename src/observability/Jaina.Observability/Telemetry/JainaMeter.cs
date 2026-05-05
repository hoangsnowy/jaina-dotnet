using System.Diagnostics.Metrics;

namespace Jaina.Observability.Telemetry;

/// <summary>
/// Single shared <see cref="Meter"/> all Jaina providers emit instruments through. OTEL
/// exporters subscribe to this meter name to capture every Jaina counter / histogram:
/// <code>
/// otel.AddMeter(JainaMeter.Name);
/// </code>
/// Instrument name convention: <c>jaina.&lt;module&gt;.&lt;noun&gt;</c> — e.g.
/// <c>jaina.outbox.pending</c> (gauge), <c>jaina.cache.get.duration</c> (histogram).
/// </summary>
public static class JainaMeter
{
    /// <summary>Meter name registered with OTEL. Stable across versions.</summary>
    public const string Name = "Jaina";

    /// <summary>The shared <see cref="Meter"/> instance.</summary>
    public static readonly Meter Instance = new(Name, "1.0.0");
}
