namespace Jaina.Observability;

public interface IOperation
{
    string? CurrentTransactionId { get; }
    void SetLabel(string label);
    void SetCustomProperty(string key, string value);
    void SetResult(string result);
    void CaptureException(Exception exception);
}
