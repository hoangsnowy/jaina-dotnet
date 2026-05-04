using Jaina.Core.Results;
using Microsoft.AspNetCore.Http;

namespace Jaina.AspNetCore;

/// <summary>
/// Maps a <see cref="Jaina.Core.Results.IResult"/> (the Jaina domain Result&lt;T&gt;) to a
/// <see cref="Microsoft.AspNetCore.Http.IResult"/> response. Status code is derived from the
/// underlying exception type (mirrors the conventions in <c>AddJainaProblemDetails</c>),
/// so endpoints that return <c>Result&lt;T&gt;</c> get consistent error shapes for free.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Convert a non-generic <see cref="Jaina.Core.Results.IResult"/> to an HTTP response.
    /// Success → 204 No Content. Failure → ProblemDetails per the standard mapping.
    /// </summary>
    public static Microsoft.AspNetCore.Http.IResult ToHttpResult(this Jaina.Core.Results.IResult result) =>
        result.IsSuccess
            ? Microsoft.AspNetCore.Http.Results.NoContent()
            : ToProblem(result);

    /// <summary>
    /// Convert a typed <see cref="Jaina.Core.Results.IResult{T}"/> to an HTTP response.
    /// Success with a value → 200 OK with the value. Success with null → 204. Failure →
    /// ProblemDetails per the standard mapping.
    /// </summary>
    public static Microsoft.AspNetCore.Http.IResult ToHttpResult<T>(this Jaina.Core.Results.IResult<T> result)
    {
        if (result.IsFailure)
            return ToProblem(result);

        return result.Value is null
            ? Microsoft.AspNetCore.Http.Results.NoContent()
            : Microsoft.AspNetCore.Http.Results.Ok(result.Value);
    }

    private static Microsoft.AspNetCore.Http.IResult ToProblem(Jaina.Core.Results.IResult result)
    {
        var (status, title) = MapException(result.Exception);
        return Microsoft.AspNetCore.Http.Results.Problem(
            detail: result.Message ?? result.Exception?.Message,
            statusCode: status,
            title: title);
    }

    private static (int Status, string Title) MapException(Exception? ex) => ex switch
    {
        ArgumentNullException => (StatusCodes.Status400BadRequest, "Bad Request"),
        ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
        KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        NotSupportedException => (StatusCodes.Status422UnprocessableEntity, "Unprocessable"),
        OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Client Closed Request"),
        null => (StatusCodes.Status400BadRequest, "Bad Request"),
        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error"),
    };
}
