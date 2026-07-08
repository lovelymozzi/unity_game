# unity-exec Error Diagnosis

## Endpoint quick map

- `GET /status`: server/queue/state snapshot (no auth)
- `POST /exec`: C# execution (auth required)
- `GET /compile`: compile status/errors/warnings (auth required)
- `GET /logs?count=50&level=all`: recent editor logs (auth required)
- `GET /security`: whitelist + forbidden patterns (auth required)

## Common failures

- `403 Invalid or missing auth token`
  - Cause: stale/missing token header.
  - Action: read token again from `~/.unity-exec/auth-token`.

- `400 Compile error`
  - Cause: syntax/type/API mismatch in submitted code.
  - Action: fix snippet; check `/compile` details for current compile health.

- `400 Security policy violation`
  - Cause: forbidden namespace/pattern (`HttpClient`, `Process.Start`, etc.).
  - Action: rewrite using allowed APIs only.

- `408 Execution timed out`
  - Cause: heavy work or blocked editor loop.
  - Action: reduce scope, split request, retry after compile idle.

- `429 Rate limit exceeded / queue is full`
  - Cause: too many requests per second or queue pressure.
  - Action: backoff and retry.

- `503 Server is stopping`
  - Cause: domain reload/editor restart in progress.
  - Action: wait for server recovery and retry.

## Diagnostic order

1. `scripts/preflight.sh`
2. `/exec` retry with minimal snippet
3. `/compile` collect errors/warnings
4. `/logs` collect recent runtime/editor errors
5. decide fix or ask confirmation before broad editor manipulation
