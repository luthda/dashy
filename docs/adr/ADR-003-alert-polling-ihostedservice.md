# ADR-003: Alert polling via IHostedService with best-effort delivery

**Date:** 2026-06-04
**Status:** Accepted

## Context

Dashy needs to check alert conditions on a user-defined interval (e.g. every 5 minutes) and push a notification to the browser when a threshold is exceeded. The notification must reach the browser tab without the user manually refreshing. The reliability requirement is explicitly best-effort: a missed alert due to process restart or crash is acceptable.

## Decision

Alert polling runs as an `IHostedService` (`AlertPollingService`) inside the .NET API process, using a `PeriodicTimer` loop. When an alert fires, the service broadcasts a Server-Sent Event (SSE) to any connected browser tab via `/api/v1/alerts/stream`. No external scheduler, message queue, or separate worker process is used.

## Alternatives considered

### Separate worker process / background job service (e.g. Hangfire, Quartz.NET)
Rejected for v1. Adds infrastructure (a job store, a separate process or service registration) for a feature the user has rated as best-effort. The added operational complexity — checking why a Hangfire job is stuck at 2am — is not justified when a missed alert is explicitly acceptable.

### Browser-side polling (setInterval in the SPA)
Rejected. Polling stops when the browser tab is closed or the machine sleeps. Since the .NET API is already running as a background service, moving polling there gives free coverage while the browser is away — even if the notification is held until the tab reconnects via SSE.

### WebSocket instead of SSE
Rejected. SSE is sufficient for the unidirectional server-push pattern (server → browser notifications only). SSE is simpler to implement in .NET (`text/event-stream` response), requires no library, and reconnects automatically if the connection drops. WebSocket would be appropriate if the browser needed to send data back over the same channel, which it does not.

## Consequences

### Positive
- Zero additional infrastructure — polling runs inside the existing API process
- SSE provides instant browser notification the moment an alert fires, with no client-side polling loop
- SSE reconnects automatically on disconnect; the browser will catch up on any firings that occurred while it was disconnected (the history panel shows the full `alert_firings` table)
- `IHostedService` lifecycle is managed by the .NET host — starts and stops cleanly with the process

### Negative / accepted tradeoffs
- If the .NET process crashes mid-check, that check is silently lost. No retry, no dead-letter queue
- If the process restarts between checks, the `last_checked_at` timestamp in the DB ensures the next check picks up correctly — but any result that was being fetched at the moment of crash is discarded
- `PeriodicTimer` fires on a fixed interval per alert; as the number of alerts grows, checks may pile up. Acceptable for personal use (expected < 20 alerts)

## Risks & mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| All alerts fire simultaneously and overwhelm log source rate limits | Low | Jitter alert check times by ±10% of the interval; process alerts sequentially per source |
| SSE connection drops silently and the user stops receiving toasts | Medium | `useAlertStream` hook reconnects on `onerror` with exponential backoff; connection status indicator in the UI |
| Alert polling runs a query while the source is temporarily unreachable, marking alert as `error` permanently | Low | Distinguish transient errors (retry next cycle) from permanent credential errors (mark `error`, notify user) |

## Open questions
- None.
