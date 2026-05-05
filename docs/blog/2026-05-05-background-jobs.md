---
title: "Background jobs that survive 1M-row reprocessing"
date: 2026-05-05
tags: [backgroundjobs, quartz, batch, microservices]
reading_time: "~6 min"
sample: samples/JainaShop/JainaShop.Notifier/Program.cs
---

# Background jobs that survive 1M-row reprocessing

## The Story

Wednesday 2 AM. A migration script needs to recompute prices for every product in the catalog (1.2M rows). Naive code: a single `await foreach` on the DB cursor. Halfway through, the SQL connection times out. The job crashes. You restart and it starts from row 1. Three retries later, the on-call engineer just SSH'd into a worker box and is running it manually. Bad night.

The fix: break the work into **idempotent chunks** scheduled by a **durable** job runner that **resumes** after a crash.

## Naive approach

```csharp
public class RecomputePricesJob : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await foreach (var p in _db.Products.AsAsyncEnumerable().WithCancellation(ct))
            p.Price = ComputeNewPrice(p);
        await _db.SaveChangesAsync(ct);   // 1.2M rows in one transaction!
    }
}
```

What breaks:

- One transaction → DB locks → other writes time out
- One process → no parallelism, no resume on crash
- `IHostedService` runs once per process; deploying a new version skips it

## Jaina solution

Two layers:

1. **Outbox** to fan out one job-per-chunk (using the existing transactional outbox)
2. **`Jaina.BackgroundJobs.Quartz`** to consume each chunk with retry + crash recovery

```csharp
// Trigger: schedule a kickoff that fans the work into 1,000-row chunks
public sealed class ChunkRecomputeJob : IBackgroundJob<ChunkPayload>
{
    private readonly AppDb _db;
    public ChunkRecomputeJob(AppDb db) => _db = db;

    public async Task ExecuteAsync(ChunkPayload payload, CancellationToken ct)
    {
        var batch = await _db.Products
            .Where(p => p.Id > payload.AfterId)
            .OrderBy(p => p.Id)
            .Take(payload.ChunkSize)
            .ToListAsync(ct);

        foreach (var p in batch) p.Price = ComputeNewPrice(p);
        await _db.SaveChangesAsync(ct);
    }
}

// Kick it off:
await scheduler.ScheduleAsync<ChunkRecomputeJob, ChunkPayload>(
    new ChunkPayload(AfterId: lastProcessedId, ChunkSize: 1000));
```

Source: [`IBackgroundJobScheduler.cs`](../../src/backgroundjobs/Jaina.BackgroundJobs/IBackgroundJobScheduler.cs), [`QuartzBackgroundJobScheduler.cs`](../../src/backgroundjobs/Jaina.BackgroundJobs.Quartz/QuartzBackgroundJobScheduler.cs).

DI:

```csharp
services.AddJainaQuartzBackgroundJobs();
services.AddTransient<IBackgroundJob<ChunkPayload>, ChunkRecomputeJob>();
```

Recurring? Cron expression:

```csharp
await scheduler.ScheduleRecurringAsync<NightlyDigestJob, NightlyArgs>(
    name: "nightly-digest",
    cronExpression: "0 0 2 * * ?",   // 2 AM daily
    payload: new NightlyArgs(...));
```

## Happy path

```
[02:00:00] queued chunk afterId=0       size=1000
[02:00:02] processed chunk: 1000 rows updated
[02:00:02] queued chunk afterId=1000    size=1000
[02:00:04] processed chunk: 1000 rows updated
...
[02:38:51] queued chunk afterId=1199000 size=1000
[02:38:53] processed chunk: 982 rows updated (last batch — < 1000)
[02:38:53] no more rows; recompute complete
```

1.2M rows in ~38 minutes, never holding more than 1k rows in a transaction.

## Error scenarios

### 1. Worker crashes mid-chunk

Quartz with persistent JobStore (SQL or RAM-with-failover) marks the job as not-acked. Another worker (or the same one after restart) picks it up. The chunk is **idempotent** because we filter `Id > AfterId` — re-running it from the same payload either finishes the unfinished portion or no-ops.

### 2. One row in the chunk has bad data, throws

Default Quartz behaviour: retry the job. Configure `MaxRetries` then move to a poison queue (manual cleanup). Or write the chunk handler to skip the bad row and continue (always log; never silently swallow).

### 3. Two workers grab the same chunk

Quartz uses a row lock in the JobStore — only one worker executes at a time. With the in-memory store, run a single worker; with the SQL JobStore, multi-instance is safe.

### 4. Schedule drift after time zone change

Use UTC cron expressions. Always.

### 5. Recurring job overlaps with itself

The previous tick is still running when the next fires. Quartz default = stack them up. Use `[DisallowConcurrentExecution]` on the job class to skip overlapping ticks.

### 6. Job leaks DI scope

Quartz job instances are constructed per-trigger. Use `IBackgroundJobScheduler`'s scope-aware adapter (handled by `Jaina.BackgroundJobs.Quartz` automatically) so each `ExecuteAsync` runs in a fresh DI scope. **Don't keep a static `DbContext`** — that's the leak.

## What you'd see in production

Useful metrics:

- `jaina.backgroundjobs.execute.duration` histogram by job name
- `jaina.backgroundjobs.failed` counter by job name + exception type
- Quartz built-in metrics: `quartz.jobs.scheduled`, `quartz.triggers.fired`

Alert on `jaina.backgroundjobs.failed{job="recompute-prices"} > 0` for > 5m.

## Trade-offs & gotchas

- **Idempotency is non-negotiable.** A retried chunk must not double-update rows. Filter by `> lastId` or store an "already processed" flag.
- **Cron resolution is one minute** in most JobStore configs. For sub-minute jobs use one-shot scheduling.
- **Persistent JobStore is required for multi-instance**. RAM JobStore loses everything on restart.
- **Long-running jobs block other jobs in the same trigger group**. Configure thread pool size, or use separate triggers per concern.
- **Outbox + BackgroundJobs is a powerful combo**: write the kickoff message in the same transaction as the domain change, then fan out chunks asynchronously. No coordination, no dual-write.

## Try it yourself

```bash
dotnet run --project samples/JainaShop/JainaShop.Notifier

# Queue an SMS dispatch (deduped via Inbox + scheduled via Quartz)
curl -X POST http://localhost:5104/events/order-placed \
     -H "Content-Type: application/json" \
     -d '{"orderId":"d4ad","sku":"WIDGET","quantity":3,"customerPhone":"+15551234"}'

# Watch logs for the SMS dispatch ~1s later
# Repeat the same call — Inbox dedups, no second SMS
```

## Further reading

- Source: [`Jaina.BackgroundJobs.Quartz`](../../src/backgroundjobs/Jaina.BackgroundJobs.Quartz/)
- Companion posts: [Outbox](2026-05-04-outbox-black-friday.md), [Inbox dedup](2026-05-05-saga-orchestration.md)
- [Quartz.NET docs](https://www.quartz-scheduler.net/documentation/)
