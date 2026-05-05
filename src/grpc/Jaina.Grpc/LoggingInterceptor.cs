using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Jaina.Grpc;

/// <summary>
/// Server-side gRPC <see cref="Interceptor"/> that logs each unary call's method, status,
/// and elapsed time. Failures are logged at <c>Error</c> with the <see cref="StatusCode"/>
/// and message; successes at <c>Information</c>.
/// </summary>
public sealed class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;
    public LoggingInterceptor(ILogger<LoggingInterceptor> logger) => _logger = logger;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await continuation(request, context);
            _logger.LogInformation("grpc {Method} OK in {Elapsed}ms", context.Method, sw.ElapsedMilliseconds);
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "grpc {Method} failed with {StatusCode} in {Elapsed}ms: {Detail}",
                context.Method, ex.StatusCode, sw.ElapsedMilliseconds, ex.Status.Detail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "grpc {Method} unhandled exception in {Elapsed}ms",
                context.Method, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
