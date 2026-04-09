namespace Jaina.Core.WebApi;

public class ErrorResponse
{
    public string? TraceId { get; set; }
    public int StatusCode { get; set; }
    public string? Message { get; set; }
    public string? Detail { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
}
