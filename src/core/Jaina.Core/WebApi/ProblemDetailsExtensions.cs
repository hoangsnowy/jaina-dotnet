using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Core.WebApi;

#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

public static class ProblemDetailsExtensions
{
    /// <summary>
    /// Registers Problem Details (RFC 7807) with standard exception-to-status mappings.
    /// Also call app.UseExceptionHandler() and app.UseStatusCodePages() after building the app.
    /// </summary>
    public static IServiceCollection AddJainaProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                var pd = ctx.ProblemDetails;
                pd.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;

                if (ctx.Exception is null)
                    return;

                (pd.Status, pd.Title, pd.Detail) = ctx.Exception switch
                {
                    ArgumentNullException ex =>
                        (StatusCodes.Status400BadRequest, "Bad Request", ex.Message),

                    ArgumentException ex =>
                        (StatusCodes.Status400BadRequest, "Bad Request", ex.Message),

                    KeyNotFoundException ex =>
                        (StatusCodes.Status404NotFound, "Not Found", ex.Message),

                    UnauthorizedAccessException ex =>
                        (StatusCodes.Status401Unauthorized, "Unauthorized", ex.Message),

                    NotSupportedException ex =>
                        (StatusCodes.Status422UnprocessableEntity, "Unprocessable", ex.Message),

                    OperationCanceledException =>
                        (StatusCodes.Status499ClientClosedRequest, "Client Closed Request", "The request was cancelled."),

                    _ => (StatusCodes.Status500InternalServerError, "Internal Server Error",
                          ctx.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()
                              ? ctx.Exception.ToString()
                              : "An unexpected error occurred.")
                };

                ctx.HttpContext.Response.StatusCode = pd.Status!.Value;
            };
        });

        return services;
    }
}
#endif
