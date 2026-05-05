---
title: "Health checks that don't fool Kubernetes: live vs ready vs startup"
date: 2026-05-05
tags: [healthchecks, kubernetes, sre]
reading_time: "~5 min"
sample: src/healthchecks/Jaina.HealthChecks/
---

# Health checks that don't fool Kubernetes: live vs ready vs startup

## The Story

Monday 8:02 AM. A node restarts. Kubernetes brings up your pod. Liveness probe says OK. Readiness probe says OK. Traffic floods in. Half the requests 500 because Redis is still warming up the cache and your pod was readiness-OK before its dependencies were. The dashboard turns red and on-call gets a page two minutes after deploy. Again.

Your `/health` endpoint just returned 200 the whole time. The bug is conflating **"process is up"** with **"ready to serve"**.

## The three types

| Probe | Question it answers | What it does in k8s |
|---|---|---|
| **Liveness** | "Is the process responsive at all?" | If fails: kill + restart pod |
| **Readiness** | "Should this pod receive traffic now?" | If fails: drop from Service endpoints; no kill |
| **Startup** | "Is the slow boot sequence done?" | If fails: keep waiting; doesn't trigger restart yet |

Conflating them is the most common mistake. `Jaina.HealthChecks` makes the split mechanical via tags.

## Jaina solution

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [JainaHealthCheckTags.Live])
    .AddCheck("redis", new RedisHealthCheck(...), tags: [JainaHealthCheckTags.Ready])
    .AddCheck("rabbit", new RabbitHealthCheck(...), tags: [JainaHealthCheckTags.Ready])
    .AddCheck("ef-context", new DbContextHealthCheck<AppDb>(), tags: [JainaHealthCheckTags.Ready]);

app.MapJainaHealthChecks();
// → /health/live   filters by tag "live"
// → /health/ready  filters by tag "ready"
```

Source: [`ApplicationBuilderExtensions.cs`](../../src/healthchecks/Jaina.HealthChecks/ApplicationBuilderExtensions.cs).

## Kubernetes wiring

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 0
  periodSeconds: 5

startupProbe:        # ASP.NET takes a few seconds to JIT + warm up
  httpGet:
    path: /health/live
  failureThreshold: 30
  periodSeconds: 2
```

## Happy path

```bash
$ curl http://localhost:5101/health/live
Healthy

$ curl http://localhost:5101/health/ready
Healthy
```

Two endpoints, two answers.

## Error scenarios

### 1. Redis blips for 200ms

The readiness probe catches it on the next 5s tick → pod drops out of the LB → no 5xx to users. Liveness still passes → pod is **not killed**. After Redis recovers, readiness goes green and traffic resumes. **No restart, no cold start, no thundering herd.**

### 2. Process deadlocked but answering port

Liveness check is a self-ping that requires the request thread to actually run. A deadlocked thread pool fails liveness → k8s restarts the pod. (You also want goroutine equivalents — periodic check ratio in Polly's pollster pattern.)

### 3. Slow startup — cache warm-up takes 30s

Startup probe waits up to `failureThreshold * periodSeconds` (60s in the example). During that window, no liveness restarts; the pod isn't in service. Once startup passes, the regular liveness/readiness probes take over. Without a startup probe, k8s might restart the pod three times before warm-up finishes.

### 4. Cascading "ready" lie

Service A's readiness depends on Service B's readiness. If B is degraded and A also returns "ready", you've created a cascade: A's pod gets traffic that immediately fails because B isn't ready. Fix: A's readiness check pings B via HTTP and **fails fast** when B is unhealthy. Use timeout / circuit-breaker so A doesn't itself hang.

### 5. Production exposes too much

`/health/ready` returns 500 with the full stack trace by default. Filter the response or expose only on a management port: `app.MapJainaHealthChecks().RequireHost("*:8081")`.

## What you'd see in production

Grafana single-stat panel:

```
sum(up{job="orders"}) / count(up{job="orders"})
```

100% = all pods ready. Drops to 75% = one pod out of four is unhealthy. Alert on `< 0.8` for > 1m.

## Trade-offs & gotchas

- **Don't put expensive checks in `/health/ready`** — k8s polls every 5s. A 2s DB check tanks your tail latency. Move slow checks to a separate observability scrape.
- **Startup probe is NOT a substitute for warm-up logic.** Use it to avoid restart loops, not to mask "the cache hasn't been built yet".
- **Liveness `200` does not mean the process is healthy.** It means it can answer one request. If the process is leaking sockets, your real users will time out long before liveness flips.

## Try it yourself

```bash
dotnet run --project samples/JainaShop/JainaShop.Catalog
curl http://localhost:5101/health/live   # process responsive
curl http://localhost:5101/health/ready  # downstream deps OK
```

Stop Redis or kill the DB connection mid-call — `/health/ready` flips to Unhealthy while `/health/live` stays Healthy. That's the split working as designed.

## Further reading

- Source: [`JainaHealthCheckTags.cs`](../../src/healthchecks/Jaina.HealthChecks/JainaHealthCheckTags.cs), [`ApplicationBuilderExtensions.cs`](../../src/healthchecks/Jaina.HealthChecks/ApplicationBuilderExtensions.cs)
- [Kubernetes probe docs](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
