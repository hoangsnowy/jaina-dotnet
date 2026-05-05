using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Jaina.Validation;

/// <summary>
/// Endpoint filter that runs the registered <see cref="IValidator{T}"/> against every
/// <c>FromBody</c>-bound argument. On failure returns
/// <see cref="StatusCodes.Status400BadRequest"/> with an RFC 7807 ProblemDetails body
/// listing each error keyed by property path. Apply via
/// <c>endpoint.AddJainaValidation()</c> or <c>group.AddJainaValidation()</c>.
/// </summary>
public sealed class JainaValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var arg in context.Arguments)
        {
            if (arg is null) continue;
            var argType = arg.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(argType);
            var validator = (IValidator?)context.HttpContext.RequestServices.GetService(validatorType);
            if (validator is null) continue;

            var ctxType = typeof(ValidationContext<>).MakeGenericType(argType);
            var ctxInstance = (FluentValidation.IValidationContext)Activator.CreateInstance(ctxType, arg)!;
            var result = await validator.ValidateAsync(ctxInstance, context.HttpContext.RequestAborted);
            if (result.IsValid) continue;

            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}

public static class JainaValidationFilterExtensions
{
    /// <summary>Attach the Jaina validation filter to a route handler.</summary>
    public static RouteHandlerBuilder AddJainaValidation(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<JainaValidationFilter>();

    /// <summary>Attach the Jaina validation filter to all endpoints in a group.</summary>
    public static RouteGroupBuilder AddJainaValidation(this RouteGroupBuilder builder) =>
        builder.AddEndpointFilter<JainaValidationFilter>();
}
