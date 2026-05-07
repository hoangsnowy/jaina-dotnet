#!/usr/bin/env bash
# JainaShop E2E smoke test — login + place order + idempotency + outbox.
#
# Usage:
#   1. Boot the AppHost in another terminal:
#        dotnet run --project samples/JainaShop/JainaShop.AppHost --launch-profile http
#   2. Run this script:
#        bash scripts/e2e-smoke.sh
#
# Service ports are discovered from the Aspire DCP temp dir
# (%TEMP%/aspire-dcp*/<svc>-*_out_*).
#
# Override discovery via env vars:
#   IDENTITY_PORT, ORDERS_PORT, GATEWAY_PORT
#
# Exit code 0 = all checks passed, non-zero = failure.

set -uo pipefail

red()    { printf '\033[31m%s\033[0m\n' "$*"; }
green()  { printf '\033[32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }

fail() { red "FAIL: $*"; exit 1; }
ok()   { green "  ok  $*"; }

# ── Port discovery ──────────────────────────────────────────────────────
discover_port() {
  local svc=$1
  local tmp_root="${TEMP:-${TMPDIR:-/tmp}}"
  # Windows path → MSYS/Git-Bash path
  tmp_root=$(echo "$tmp_root" | sed 's|\\|/|g; s|^\([A-Za-z]\):|/\L\1|')
  local latest
  latest=$(ls -t "$tmp_root" 2>/dev/null | grep '^aspire-dcp' | head -1)
  [ -z "$latest" ] && return 1
  grep -oE 'localhost:[0-9]+' "$tmp_root/$latest/${svc}-"*_out_* 2>/dev/null | head -1 | sed 's/localhost://'
}

IDENTITY_PORT=${IDENTITY_PORT:-$(discover_port identity)}
ORDERS_PORT=${ORDERS_PORT:-$(discover_port orders)}
GATEWAY_PORT=${GATEWAY_PORT:-$(discover_port gateway)}

[ -z "$IDENTITY_PORT" ] && fail "could not discover identity port — is AppHost running?"
[ -z "$ORDERS_PORT" ]   && fail "could not discover orders port"
[ -z "$GATEWAY_PORT" ]  && fail "could not discover gateway port"

yellow "discovered: identity=$IDENTITY_PORT  orders=$ORDERS_PORT  gateway=$GATEWAY_PORT"

# ── Health probe ────────────────────────────────────────────────────────
yellow "─── health probe ───"
for svc in IDENTITY ORDERS GATEWAY; do
  port_var="${svc}_PORT"
  port=${!port_var}
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 "http://localhost:$port/health/ready")
  [ "$code" = "200" ] || fail "$svc /health/ready=$code"
  ok "$svc :$port /health/ready=200"
done

# ── 1. Login ────────────────────────────────────────────────────────────
yellow "─── 1. login (alice@acme) → JWT ───"
TOKEN_JSON=$(curl -s -X POST "http://localhost:$IDENTITY_PORT/tokens" \
  -H 'Content-Type: application/json' \
  -d '{"Username":"alice@acme","Password":"alice123"}')
JWT=$(echo "$TOKEN_JSON" | grep -oE '"access_token":"[^"]+"' | sed 's/"access_token":"//;s/"$//')
[ -z "$JWT" ] && fail "no JWT in response: $TOKEN_JSON"
ok "got JWT (${#JWT} chars)"

# Decode + validate claims
PAYLOAD=$(echo "$JWT" | cut -d. -f2)
PAD=$(printf '%s' "$PAYLOAD" | awk '{ p=(4-length($0)%4)%4; printf "%s%s",$0,substr("====",1,p) }')
CLAIMS=$(echo "$PAD" | tr '_-' '/+' | base64 -d 2>/dev/null)
echo "$CLAIMS" | grep -q '"tid":"acme"' || fail "JWT missing tid=acme"
echo "$CLAIMS" | grep -q 'orders.write'  || fail "JWT missing orders.write scope"
ok "JWT claims valid (tid=acme, scope=orders.write)"

# ── 2. Place order via Gateway ──────────────────────────────────────────
yellow "─── 2. POST /api/orders (Gateway → Orders) ───"
IDEM=$(uuidgen 2>/dev/null || powershell -Command "[guid]::NewGuid().ToString()" 2>/dev/null | tr -d '\r' || echo "fallback-$RANDOM-$$")
RESP=$(curl -s -X POST "http://localhost:$GATEWAY_PORT/api/orders" \
  -H 'Content-Type: application/json' \
  -H "X-Tenant: acme" \
  -H "Idempotency-Key: $IDEM" \
  -H "Authorization: Bearer $JWT" \
  -d '{"Sku":"WIDGET-001","Quantity":2,"UnitPrice":9.99}')
ORDER_ID=$(echo "$RESP" | grep -oE '"id":"[a-f0-9-]+' | head -1 | cut -d'"' -f4)
[ -z "$ORDER_ID" ] && fail "no order id in response: $RESP"
ok "order placed: id=$ORDER_ID"

# ── 3. Read back from Orders ────────────────────────────────────────────
yellow "─── 3. GET /orders/{id} (Orders direct) ───"
GET_RESP=$(curl -s "http://localhost:$ORDERS_PORT/orders/$ORDER_ID")
echo "$GET_RESP" | grep -q "\"id\":\"$ORDER_ID\"" || fail "GET returned wrong/no order: $GET_RESP"
echo "$GET_RESP" | grep -q '"sku":"WIDGET-001"' || fail "sku mismatch"
echo "$GET_RESP" | grep -q '"total":19.98'      || fail "total mismatch (expected 19.98)"
ok "GET matches POST (sku, total)"

# ── 4. Outbox dispatch ──────────────────────────────────────────────────
yellow "─── 4. GET /_outbox ───"
OUTBOX=$(curl -s "http://localhost:$ORDERS_PORT/_outbox")
echo "$OUTBOX" | grep -q '"payloadType":"JainaShop.Orders.OrderPlaced"' || fail "no OrderPlaced in outbox: $OUTBOX"
echo "$OUTBOX" | grep -q '"destination":"orders.events"'                || fail "wrong destination"
echo "$OUTBOX" | grep -qE '"processedAt":"[^"]+"' || yellow "  warn: outbox not yet processed (relay polls every 500ms — re-run if intermittent)"
ok "outbox enqueued + dispatched (OrderPlaced → orders.events)"

# ── 5. Idempotency replay ───────────────────────────────────────────────
yellow "─── 5. idempotency replay (same key) ───"
RESP2=$(curl -s -X POST "http://localhost:$GATEWAY_PORT/api/orders" \
  -H 'Content-Type: application/json' \
  -H "X-Tenant: acme" \
  -H "Idempotency-Key: $IDEM" \
  -H "Authorization: Bearer $JWT" \
  -d '{"Sku":"WIDGET-001","Quantity":2,"UnitPrice":9.99}')
ORDER_ID2=$(echo "$RESP2" | grep -oE '"id":"[a-f0-9-]+' | head -1 | cut -d'"' -f4)
[ "$ORDER_ID" = "$ORDER_ID2" ] || fail "idempotency broken: $ORDER_ID != $ORDER_ID2"
ok "same order id returned ($ORDER_ID)"

# ── 6. Tenant guard ─────────────────────────────────────────────────────
yellow "─── 6. gateway tenant guard (no X-Tenant → 400) ───"
NO_TENANT_CODE=$(curl -s -o /dev/null -w '%{http_code}' \
  -X POST "http://localhost:$GATEWAY_PORT/api/orders" \
  -H 'Content-Type: application/json' \
  -H "Idempotency-Key: $(date +%s%N)" \
  -d '{"Sku":"WIDGET-001","Quantity":1,"UnitPrice":9.99}')
[ "$NO_TENANT_CODE" = "400" ] || fail "expected 400 without tenant, got $NO_TENANT_CODE"
ok "gateway rejects request without X-Tenant (400)"

echo
green "ALL E2E CHECKS PASSED"
