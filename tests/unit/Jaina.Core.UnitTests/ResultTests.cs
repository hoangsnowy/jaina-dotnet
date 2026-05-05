using Jaina.Core.Results;

namespace Jaina.Core.UnitTests;

public class ResultTests
{
    [Fact]
    public void Ok_SetsIsSuccess()
    {
        // Act
        var result = Result.Ok();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Ok_WithMessage_SetsMessage()
    {
        // Act
        var result = Result.Ok("done");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("done", result.Message);
    }

    [Fact]
    public void Fail_WithMessage_SetsIsFailure()
    {
        // Act
        var result = Result.Fail("something went wrong");

        // Assert
        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal("something went wrong", result.Message);
    }

    [Fact]
    public void Fail_WithException_SetsMessageFromException()
    {
        // Arrange
        var ex = new Exception("boom");

        // Act
        var result = Result.Fail(ex);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ex.Message, result.Message);
    }

    [Fact]
    public void OkT_SetsValueAndIsSuccess()
    {
        // Act
        var result = Result.Ok(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void FailT_WithMessage_SetsIsFailureAndDefaultValue()
    {
        // Act
        var result = Result.Fail<int>("not found");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("not found", result.Message);
        Assert.Equal(default, result.Value);
    }

    [Fact]
    public void FailT_WithException_SetsIsFailure()
    {
        // Arrange
        var ex = new Exception("error");

        // Act
        var result = Result.Fail<string>(ex);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ex.Message, result.Message);
    }

    [Fact]
    public void Equality_TwoOkResultsWithSameMessage_AreEqual()
    {
        // Arrange / Act
        var a = Result.Ok("msg");
        var b = Result.Ok("msg");

        // Assert
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_OkAndFail_AreNotEqual()
    {
        // Arrange / Act
        var ok = Result.Ok();
        var fail = Result.Fail("error");

        // Assert
        Assert.NotEqual(ok, fail);
        Assert.True(ok != fail);
    }

    [Fact]
    public async Task OkAsync_ReturnsSuccessResult()
    {
        // Act
        var result = await Result.OkAsync("async done");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("async done", result.Message);
    }

    [Fact]
    public async Task FailAsync_ReturnsFailureResult()
    {
        // Act
        var result = await Result.FailAsync("async error");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("async error", result.Message);
    }
}
