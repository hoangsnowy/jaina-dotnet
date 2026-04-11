using NLog;

namespace Jaina.Diagnostics.NLog;

internal sealed class NLogSpan : Jaina.Diagnostics.ISpan
{
    private readonly ILogger _logger;
    private readonly string _name;
    private readonly string _type;
    private readonly DateTimeOffset _start;
    private readonly Dictionary<string, string> _labels = new();

    public NLogSpan(ILogger logger, string name, string type, string? subType, string? action)
    {
        _logger = logger;
        _name = name;
        _type = type;
        _start = DateTimeOffset.UtcNow;

        _logger.Debug("Span started | name={SpanName} type={SpanType} subType={SubType} action={Action}",
            name, type, subType ?? "-", action ?? "-");
    }

    public void SetLabel(string key, string value) => _labels[key] = value;

    public void CaptureException(Exception exception) =>
        _logger.Error(exception, "Exception in span {SpanName}: {Message}", _name, exception.Message);

    public void Dispose()
    {
        var duration = DateTimeOffset.UtcNow - _start;
        var labels = _labels.Count > 0
            ? string.Join(", ", _labels.Select(kv => $"{kv.Key}={kv.Value}"))
            : "(none)";

        _logger.Debug("Span ended | name={SpanName} type={SpanType} durationMs={DurationMs} labels={Labels}",
            _name, _type, (long)duration.TotalMilliseconds, labels);
    }
}
