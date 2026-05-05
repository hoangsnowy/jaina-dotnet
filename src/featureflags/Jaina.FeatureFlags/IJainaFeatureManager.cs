using Jaina.Observability.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

namespace Jaina.FeatureFlags;

/// <summary>
/// Strongly-typed wrapper over <see cref="IFeatureManager"/> that emits an OTEL span per
/// flag evaluation (tagged with the resolved value) and writes a debug log line. Use this
/// instead of <c>IFeatureManager</c> directly to get observable flag rollouts for free.
/// </summary>
public interface IJainaFeatureManager
{
    Task<bool> IsEnabledAsync(string feature, CancellationToken ct = default);
}

internal sealed class JainaFeatureManager : IJainaFeatureManager
{
    private readonly IFeatureManager _inner;
    private readonly ILogger<JainaFeatureManager> _logger;

    public JainaFeatureManager(IFeatureManager inner, ILogger<JainaFeatureManager> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(string feature, CancellationToken ct = default)
    {
        using var span = JainaActivitySource.StartSpan("featureflags", "evaluate");
        span?.SetTag("jaina.featureflag.name", feature);

        var enabled = await _inner.IsEnabledAsync(feature);
        span?.SetTag("jaina.featureflag.enabled", enabled);
        _logger.LogDebug("Feature flag {Feature} = {Enabled}", feature, enabled);

        return enabled;
    }
}
