using Jaina.AspNetCore;
using Jaina.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jaina.AspNetCore.UnitTests;

public class ResultExtensionsTests
{
    [Fact]
    public void ToHttpResult_SuccessNoValue_Returns204()
    {
        var http = Result.Ok().ToHttpResult();
        Assert.IsType<NoContent>(http);
    }

    [Fact]
    public void ToHttpResult_SuccessWithValue_Returns200WithValue()
    {
        var http = Result.Ok<string>("hello").ToHttpResult();
        var ok = Assert.IsType<Ok<string>>(http);
        Assert.Equal("hello", ok.Value);
    }

    [Fact]
    public void ToHttpResult_SuccessWithNullValue_Returns204()
    {
        var http = Result.Ok<string>().ToHttpResult();
        Assert.IsType<NoContent>(http);
    }

    [Fact]
    public void ToHttpResult_FailureWithKeyNotFound_Returns404Problem()
    {
        var http = Result.Fail<string>(new KeyNotFoundException("missing")).ToHttpResult();
        var problem = Assert.IsType<ProblemHttpResult>(http);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public void ToHttpResult_FailureWithArgumentException_Returns400Problem()
    {
        var http = Result.Fail<string>(new ArgumentException("bad")).ToHttpResult();
        var problem = Assert.IsType<ProblemHttpResult>(http);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public void ToHttpResult_FailureWithUnauthorized_Returns401Problem()
    {
        var http = Result.Fail<string>(new UnauthorizedAccessException("nope")).ToHttpResult();
        var problem = Assert.IsType<ProblemHttpResult>(http);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    [Fact]
    public void ToHttpResult_FailureWithNotSupported_Returns422Problem()
    {
        var http = Result.Fail<string>(new NotSupportedException("nope")).ToHttpResult();
        var problem = Assert.IsType<ProblemHttpResult>(http);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
    }

    [Fact]
    public void ToHttpResult_FailureWithGenericException_Returns500Problem()
    {
        var http = Result.Fail<string>(new InvalidOperationException("boom")).ToHttpResult();
        var problem = Assert.IsType<ProblemHttpResult>(http);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }

    [Fact]
    public void ToHttpResult_FailureWithoutException_DefaultsTo400()
    {
        // No exception — message-only failure → 400
        var http = Result.Fail<string>("invalid input").ToHttpResult();
        var problem = Assert.IsType<ProblemHttpResult>(http);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Contains("invalid input", problem.ProblemDetails.Detail);
    }
}
