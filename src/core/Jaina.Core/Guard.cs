using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Jaina.Core;

public static class Guard
{
#if NET6_0_OR_GREATER
    public static T NotNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null)
            throw new ArgumentNullException(paramName, $"{paramName} must not be null");
        return value;
    }

    public static string NotNullOrEmpty([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException($"{paramName} must not be null or empty", paramName);
        return value;
    }

    public static string NotNullOrWhiteSpace([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} must not be null or whitespace", paramName);
        return value;
    }

    public static IEnumerable<T> NotNullOrEmpty<T>([NotNull] IEnumerable<T>? source, [CallerArgumentExpression(nameof(source))] string? paramName = null)
    {
        if (source is null || !source.Any())
            throw new ArgumentException("The collection must not be null or empty.", paramName);
        return source;
    }

    public static void IsValidDate(string value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!DateTime.TryParse(value, out _))
            throw new ArgumentException($"Please enter a valid date for {paramName}", paramName);
    }
#else
    public static T NotNull<T>(T? value, string? paramName = null)
    {
        if (value is null)
            throw new ArgumentNullException(paramName, $"{paramName} must not be null");
        return value;
    }

    public static string NotNullOrEmpty(string? value, string? paramName = null)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException($"{paramName} must not be null or empty", paramName);
        return value!;
    }

    public static string NotNullOrWhiteSpace(string? value, string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} must not be null or whitespace", paramName);
        return value!;
    }

    public static IEnumerable<T> NotNullOrEmpty<T>(IEnumerable<T>? source, string? paramName = null)
    {
        if (source is null || !source.Any())
            throw new ArgumentException("The collection must not be null or empty.", paramName);
        return source;
    }

    public static void IsValidDate(string value, string? paramName = null)
    {
        if (!DateTime.TryParse(value, out _))
            throw new ArgumentException($"Please enter a valid date for {paramName}", paramName);
    }
#endif

    public static void Requires<TException>(bool condition, string? message = null, Exception? innerException = null)
        where TException : Exception
    {
        if (!condition)
        {
            var exception = (TException?)Activator.CreateInstance(typeof(TException), message ?? string.Empty, innerException);
            if (exception is null)
                throw new InvalidOperationException(message);
            throw exception;
        }
    }
}
