using Microsoft.AspNetCore.Mvc.Filters;

namespace Jaina.Diagnostics;

public class OperationActionFilter : IAsyncActionFilter
{
    private readonly IOperation _operation;

    public OperationActionFilter(IOperation operation)
    {
        _operation = operation;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var actionName = context.ActionDescriptor.DisplayName;
        _operation.SetLabel(actionName ?? "Unknown");

        var result = await next();

        if (result.Exception is not null)
        {
            _operation.CaptureException(result.Exception);
            _operation.SetResult("failure");
        }
        else
        {
            _operation.SetResult("success");
        }
    }
}
