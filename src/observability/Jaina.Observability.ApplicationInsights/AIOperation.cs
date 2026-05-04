using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Jaina.Observability.ApplicationInsights;

public class AIOperation : IOperation
{
    private readonly TelemetryClient _client;

    public AIOperation(TelemetryClient client)
    {
        _client = client;
    }

    public string? CurrentTransactionId => Activity.Current?.TraceId.ToString();

    public void SetLabel(string label) =>
        _client.Context.Operation.Name = label;

    public void SetCustomProperty(string key, string value) =>
        _client.Context.GlobalProperties[key] = value;

    public void SetResult(string result) =>
        _client.TrackTrace($"Result: {result}", SeverityLevel.Information);

    public void CaptureException(Exception exception) =>
        _client.TrackException(exception);
}
