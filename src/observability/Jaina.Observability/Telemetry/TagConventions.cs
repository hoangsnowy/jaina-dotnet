namespace Jaina.Observability.Telemetry;

/// <summary>
/// Standard OTEL attribute names used across Jaina providers. Stick to these so dashboards
/// and alerts work uniformly regardless of which Jaina module emitted the span.
/// </summary>
public static class TagConventions
{
    // ── Common ─────────────────────────────────────────────────────────
    public const string TenantId      = "jaina.tenant.id";
    public const string CorrelationId = "jaina.correlation.id";
    public const string UserId        = "jaina.user.id";

    // ── Cache (jaina.cache.*) ──────────────────────────────────────────
    public const string CacheKey      = "jaina.cache.key";
    public const string CacheHit      = "jaina.cache.hit";
    public const string CacheProvider = "jaina.cache.provider";

    // ── Messaging (jaina.queue.* / jaina.outbox.* / jaina.inbox.* / jaina.saga.*) ──
    public const string MessageId        = "jaina.message.id";
    public const string MessageType      = "jaina.message.type";
    public const string Destination      = "jaina.message.destination";
    public const string OutboxAttempt    = "jaina.outbox.attempt";
    public const string SagaCorrelation  = "jaina.saga.correlation_id";
    public const string SagaStep         = "jaina.saga.step";

    // ── Idempotency (jaina.idempotency.*) ──────────────────────────────
    public const string IdempotencyKey   = "jaina.idempotency.key";
    public const string IdempotencyReplay = "jaina.idempotency.replay";

    // ── Resilience (jaina.resilience.*) ────────────────────────────────
    public const string ResiliencePipeline = "jaina.resilience.pipeline";
    public const string ResilienceAttempt  = "jaina.resilience.attempt";
}
