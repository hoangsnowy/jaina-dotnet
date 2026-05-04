using Elastic.Apm;
using Elastic.Apm.Api;

namespace Jaina.Observability.ElasticApm;

public class ApmTelemetry : ITelemetry
{
    public ISpan StartSpan(string name, string type, string? subType = null, string? action = null)
    {
        var span = Agent.Tracer.CurrentTransaction?.StartSpan(name, type, subType, action);
        return new ApmSpan(span);
    }
}

internal class ApmSpan : ISpan
{
    private readonly Elastic.Apm.Api.ISpan? _span;

    public ApmSpan(Elastic.Apm.Api.ISpan? span) => _span = span;

    public void SetLabel(string key, string value) => _span?.SetLabel(key, value);
    public void CaptureException(Exception exception) => _span?.CaptureException(exception);
    public void Dispose() => _span?.End();
}
