using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Jaina.Diagnostics.ApplicationInsights;

public class AITelemetry : ITelemetry
{
    private readonly TelemetryClient _client;

    public AITelemetry(TelemetryClient client)
    {
        _client = client;
    }

    public ISpan StartSpan(string name, string type, string? subType = null, string? action = null) =>
        new AISpan(_client, name, type);
}

internal class AISpan : ISpan
{
    private readonly TelemetryClient _client;
    private readonly DependencyTelemetry _dependency;
    private readonly DateTimeOffset _start;

    public AISpan(TelemetryClient client, string name, string type)
    {
        _client = client;
        _start = DateTimeOffset.UtcNow;
        _dependency = new DependencyTelemetry { Name = name, Type = type };
    }

    public void SetLabel(string key, string value) => _dependency.Properties[key] = value;
    public void CaptureException(Exception exception) => _client.TrackException(exception);

    public void Dispose()
    {
        _dependency.Duration = DateTimeOffset.UtcNow - _start;
        _dependency.Timestamp = _start;
        _dependency.Success = true;
        _client.TrackDependency(_dependency);
    }
}
