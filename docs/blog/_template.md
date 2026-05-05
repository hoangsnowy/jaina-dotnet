---
title: "{{ Title — punchy, business-flavoured }}"
date: YYYY-MM-DD
tags: [pattern1, pattern2]
author: "{{ Author }}"
reading_time: "~{{ N }} min"
sample_branch: samples/blog/{{ slug }}
---

# {{ Title }}

## The Story

{{ One vivid paragraph describing a real-world incident: peak traffic, retry storm, partial failure, etc. Avoid jargon. The reader should feel the pain. }}

## Naive approach

```csharp
// Typical first-pass code
```

What breaks: {{ specific failure mode the naive code can't handle }}.

## Jaina solution

```csharp
// Code lifted from samples/JainaShop/JainaShop.AppHost
```

Wiring (`Program.cs`):

```csharp
builder.Services.AddJaina<Module>();
```

## Happy path

Walk through one concrete request. Show response status, headers, body.

```bash
curl -X POST http://localhost:5000/api/... \
  -H "Idempotency-Key: order-42" \
  -d '{"sku":"abc"}'
```

Expected:

```http
HTTP/1.1 201 Created
Content-Type: application/json
{"orderId":"...","sku":"abc"}
```

## Error scenarios (mandatory — at least 4)

### 1. {{ Failure mode A — e.g. network timeout }}

```bash
# how to reproduce
```

What Jaina does: {{ specific behaviour }}.
What the user sees: {{ status code + body }}.
What the operator sees in logs/traces: {{ }}.

### 2. {{ Failure mode B — e.g. duplicate request with same key }}

…

### 3. {{ Failure mode C — e.g. downstream broker outage }}

…

### 4. {{ Failure mode D — e.g. poison message (max attempts exhausted) }}

…

## What you'd see in production

- **OTEL trace** — {{ screenshot or ASCII tree of the spans }}
- **Logs** — sample lines with the relevant fields highlighted
- **Metrics** — which counter / histogram tells you the failure rate

## Trade-offs & gotchas

- {{ Honest weakness #1 }}
- {{ Honest weakness #2 }}
- {{ When you'd reach for a different pattern instead }}

## Try it yourself

```bash
git clone https://github.com/HoangSnowy/jaina-dotnet
cd jaina-dotnet
git checkout {{ sample_branch }}
dotnet run --project samples/JainaShop/JainaShop.AppHost
```

Then run the curl scripts above against `http://localhost:5000`.

## Further reading

- Source: [`{{ path/to/source }}`](../../path)
- Tests: [`{{ path/to/tests }}`](../../path)
- Plan: see `~/.claude/plans/...` (architecture decisions)
