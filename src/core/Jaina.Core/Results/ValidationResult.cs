namespace Jaina.Core.Results;

public readonly struct ValidationResult : IResult, IEquatable<ValidationResult>
{
    internal ValidationResult(bool isSuccess, string? message, Exception? exception, IDictionary<string, string[]>? errors)
    {
        IsSuccess = isSuccess;
        IsFailure = !isSuccess;
        Message = message;
        Exception = exception;
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public string? Message { get; }
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public Exception? Exception { get; }
    public IDictionary<string, string[]> Errors { get; }

    public static ValidationResult Ok() => new(true, null, null, null);
    public static ValidationResult Ok(string message) => new(true, message, null, null);

    public static ValidationResult Fail(string message) => new(false, message, null, null);
    public static ValidationResult Fail(string message, IDictionary<string, string[]> errors) => new(false, message, null, errors);
    public static ValidationResult Fail(IDictionary<string, string[]> errors) => new(false, null, null, errors);

    public bool Equals(ValidationResult other) =>
        Message == other.Message && IsSuccess == other.IsSuccess;

    public override bool Equals(object? obj) =>
        obj is ValidationResult result && Equals(result);

    public override int GetHashCode()
    {
#if NET6_0_OR_GREATER
        return HashCode.Combine(Message, IsSuccess);
#else
        return ((Message?.GetHashCode() ?? 0) * 397) ^ IsSuccess.GetHashCode();
#endif
    }

    public static bool operator ==(ValidationResult left, ValidationResult right) => left.Equals(right);
    public static bool operator !=(ValidationResult left, ValidationResult right) => !left.Equals(right);
}
