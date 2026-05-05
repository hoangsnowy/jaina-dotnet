namespace Jaina.Idempotency;

/// <summary>
/// A captured HTTP-style response stored against an idempotency key.
/// </summary>
/// <param name="StatusCode">HTTP status code from the original execution.</param>
/// <param name="ContentType">Response Content-Type header, may be null for empty bodies.</param>
/// <param name="Body">Raw response body bytes; empty array when there was no body.</param>
/// <param name="CreatedAt">UTC timestamp when the entry was first written.</param>
public sealed record IdempotencyEntry(
    int StatusCode,
    string? ContentType,
    byte[] Body,
    DateTimeOffset CreatedAt);
