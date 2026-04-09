using Elastic.Apm;
using Elastic.Apm.Api;

namespace Jaina.Diagnostics.ElasticApm;

public class ApmOperation : IOperation
{
    public string? CurrentTransactionId => Agent.Tracer.CurrentTransaction?.Id;

    public void SetLabel(string label) =>
        Agent.Tracer.CurrentTransaction?.SetLabel("label", label);

    public void SetCustomProperty(string key, string value) =>
        Agent.Tracer.CurrentTransaction?.SetLabel(key, value);

    public void SetResult(string result)
    {
        var tx = Agent.Tracer.CurrentTransaction;
        if (tx is not null) tx.Result = result;
    }

    public void CaptureException(Exception exception) =>
        Agent.Tracer.CurrentTransaction?.CaptureException(exception);
}
