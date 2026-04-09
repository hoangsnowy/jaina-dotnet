namespace Jaina.Core.Results;

public readonly struct Result : IResult, IEquatable<Result>
{
    internal Result(bool isSuccess, string? message, Exception? exception)
    {
        IsSuccess = isSuccess;
        IsFailure = !isSuccess;
        Message = message;
        Exception = exception;
    }

    public string? Message { get; }
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public Exception? Exception { get; }

    public static Result Ok() => new(true, null, null);
    public static Result Ok(string message) => new(true, message, null);
    public static Result<T> Ok<T>() => new(true, null, default, null);
    public static Result<T> Ok<T>(T value) => new(true, null, value, null);
    public static Result<T> Ok<T>(string message, T value) => new(true, message, value, null);

    public static Task<Result> OkAsync() => Task.FromResult(Ok());
    public static Task<Result> OkAsync(string message) => Task.FromResult(Ok(message));
    public static Task<Result<T>> OkAsync<T>() => Task.FromResult(Ok<T>());
    public static Task<Result<T>> OkAsync<T>(T value) => Task.FromResult(Ok(value));
    public static Task<Result<T>> OkAsync<T>(string message, T value) => Task.FromResult(Ok(message, value));

    public static Result Fail(Exception exception) => new(false, exception.Message, exception);
    public static Result Fail(string message) => new(false, message, null);
    public static Result Fail(string message, Exception exception) => new(false, message, exception);
    public static Result<T> Fail<T>(Exception exception) => new(false, exception.Message, default, exception);
    public static Result<T> Fail<T>(string message) => new(false, message, default, null);
    public static Result<T> Fail<T>(string message, Exception exception) => new(false, message, default, exception);
    public static Result<T> Fail<T>(T value) => new(false, string.Empty, value, null);
    public static Result<T> Fail<T>(string message, T value) => new(false, message, value, null);
    public static Result<T> Fail<T>(string message, T value, Exception exception) => new(false, message, value, exception);

    public static Task<Result> FailAsync(Exception exception) => Task.FromResult(Fail(exception));
    public static Task<Result> FailAsync(string message) => Task.FromResult(Fail(message));
    public static Task<Result> FailAsync(string message, Exception exception) => Task.FromResult(Fail(message, exception));
    public static Task<Result<T>> FailAsync<T>(Exception exception) => Task.FromResult(Fail<T>(exception));
    public static Task<Result<T>> FailAsync<T>(string message) => Task.FromResult(Fail<T>(message));
    public static Task<Result<T>> FailAsync<T>(string message, Exception exception) => Task.FromResult(Fail<T>(message, exception));
    public static Task<Result<T>> FailAsync<T>(T value) => Task.FromResult(Fail(value));
    public static Task<Result<T>> FailAsync<T>(string message, T value) => Task.FromResult(Fail(message, value));
    public static Task<Result<T>> FailAsync<T>(string message, T value, Exception exception) => Task.FromResult(Fail(message, value, exception));

    public bool Equals(Result other) =>
        Message == other.Message && IsSuccess == other.IsSuccess;

    public override bool Equals(object? obj) =>
        obj is Result result && Equals(result);

    public override int GetHashCode()
    {
#if NET6_0_OR_GREATER
        return HashCode.Combine(Message, IsSuccess);
#else
        return ((Message?.GetHashCode() ?? 0) * 397) ^ IsSuccess.GetHashCode();
#endif
    }

    public static bool operator ==(Result left, Result right) => left.Equals(right);
    public static bool operator !=(Result left, Result right) => !left.Equals(right);
}

public readonly struct Result<T> : IResult<T>, IEquatable<Result<T>>
{
    internal Result(bool isSuccess, string? message, T? value, Exception? exception)
    {
        IsSuccess = isSuccess;
        IsFailure = !isSuccess;
        Message = message;
        Value = value;
        Exception = exception;
    }

    public string? Message { get; }
    public T? Value { get; }
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public Exception? Exception { get; }

    public bool Equals(Result<T> other) =>
        Message == other.Message &&
        EqualityComparer<T>.Default.Equals(Value!, other.Value!) &&
        IsSuccess == other.IsSuccess;

    public override bool Equals(object? obj) =>
        obj is Result<T> result && Equals(result);

    public override int GetHashCode()
    {
#if NET6_0_OR_GREATER
        return HashCode.Combine(Message, Value, IsSuccess);
#else
        unchecked
        {
            int hash = (Message?.GetHashCode() ?? 0) * 397;
            hash ^= (Value?.GetHashCode() ?? 0);
            hash = hash * 397 ^ IsSuccess.GetHashCode();
            return hash;
        }
#endif
    }

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);
}
