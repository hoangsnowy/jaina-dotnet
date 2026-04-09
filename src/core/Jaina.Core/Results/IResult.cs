namespace Jaina.Core.Results;

public interface IResult
{
    string? Message { get; }
    bool IsSuccess { get; }
    bool IsFailure { get; }
    Exception? Exception { get; }
}

public interface IResult<out T> : IResult
{
    T? Value { get; }
}
