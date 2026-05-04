using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jaina.AspNetCore;

/// <summary>
/// Endpoint filter that detects a returned <see cref="Jaina.Core.Results.IResult"/> and
/// converts it into an HTTP response via <see cref="ResultExtensions.ToHttpResult"/>.
/// Apply with <c>endpoint.AddEndpointFilter&lt;JainaResultEndpointFilter&gt;()</c> or
/// <c>endpoint.WithJainaResultFilter()</c>.
/// </summary>
public sealed class JainaResultEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var raw = await next(context);

        return raw switch
        {
            Jaina.Core.Results.IResult typed when raw.GetType() is { IsGenericType: true } t
                && t.GetGenericTypeDefinition() == typeof(Jaina.Core.Results.Result<>)
                => InvokeTyped(typed, t),
            Jaina.Core.Results.IResult plain => plain.ToHttpResult(),
            _ => raw,
        };
    }

    private static Microsoft.AspNetCore.Http.IResult InvokeTyped(Jaina.Core.Results.IResult result, Type resultType)
    {
        // Result<T> implements IResult<T> via inheritance; IsSuccess + Value present.
        // Use the non-generic ToHttpResult overload — it inspects IsSuccess and falls back to
        // ToHttpResult<object> behaviour because the underlying IResult also exposes Value.
        var valueProp = resultType.GetProperty("Value");
        if (result.IsFailure || valueProp is null)
            return result.ToHttpResult();

        var value = valueProp.GetValue(result);
        return value is null
            ? Microsoft.AspNetCore.Http.Results.NoContent()
            : Microsoft.AspNetCore.Http.Results.Ok(value);
    }
}

public static class JainaResultEndpointFilterExtensions
{
    /// <summary>Attach the Jaina Result→HTTP filter to a route handler.</summary>
    public static RouteHandlerBuilder WithJainaResultFilter(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<JainaResultEndpointFilter>();

    /// <summary>Attach the Jaina Result→HTTP filter to all endpoints in a group.</summary>
    public static RouteGroupBuilder WithJainaResultFilter(this RouteGroupBuilder builder) =>
        builder.AddEndpointFilter<JainaResultEndpointFilter>();
}
