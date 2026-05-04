namespace Jaina.Observability;

public interface ITelemetry
{
    ISpan StartSpan(string name, string type, string? subType = null, string? action = null);
}

public interface ISpan : IDisposable
{
    void SetLabel(string key, string value);
    void CaptureException(Exception exception);
}
