using NLog;

namespace Jaina.Diagnostics.NLog;

public class NLogTelemetry : Jaina.Diagnostics.ITelemetry
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    public Jaina.Diagnostics.ISpan StartSpan(string name, string type, string? subType = null, string? action = null) =>
        new NLogSpan(Logger, name, type, subType, action);
}
