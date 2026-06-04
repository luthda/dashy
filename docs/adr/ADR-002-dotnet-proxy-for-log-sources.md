# ADR-002: .NET API as proxy for all log source queries

**Date:** 2026-06-04
**Status:** Accepted

## Context

The React Vite SPA needs to query Azure App Insights (`api.applicationinsights.io`) and self-hosted Loki instances. The original design proposed a .NET 10 API proxy with "credential security" as the primary justification. On a single-machine local deployment, that justification is weak — a local attacker with access to the browser process also has access to the .NET process. The real reason a proxy is required is **CORS**: neither App Insights nor a typical Loki deployment will include the browser's localhost origin in their `Access-Control-Allow-Origin` headers, making direct browser fetch calls fail.

## Decision

The .NET 10 Web API will proxy all log source queries. API keys and connection details are stored in the database and never returned to the browser. The documented justification is CORS bypass, not credential security theatre.

## Alternatives considered

### Direct browser fetch (no proxy)
Rejected. `api.applicationinsights.io` does not include `http://localhost:5173` in its CORS policy. Loki deployments vary but most default configurations do not either. Direct calls would fail in the browser without a proxy or a custom Loki CORS configuration the user may not control.

### Tauri / Electron desktop runtime
Would bypass browser CORS restrictions by using a native HTTP client instead of `fetch`. Rejected for v1 because it requires a separate build pipeline and significantly increases packaging complexity. Reconsidering this is a valid v2 decision if a desktop app becomes the target distribution model — at that point the .NET backend could be embedded or eliminated.

### Node.js / Express proxy instead of .NET
Rejected. The project already has .NET 10 as the backend stack. Adding a Node proxy would introduce a second runtime, second language, and second dependency tree for no capability gain.

## Consequences

### Positive
- CORS problem is solved unconditionally — no requirement on the log source's CORS configuration
- All log source query logic lives in one place (the .NET API); the SPA is source-agnostic
- API keys are held server-side and never appear in browser network traffic (a genuine secondary benefit, even if not the primary justification)
- Adding future log sources (CloudWatch, GCP) requires only a new .NET adapter, not frontend changes

### Negative / accepted tradeoffs
- The .NET API process must be running for the SPA to function — one more thing to start and monitor locally
- All log queries incur an extra hop (browser → .NET → log source) adding latency. At LAN/localhost speeds this is negligible

## Risks & mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| .NET proxy becomes a latency bottleneck for large result sets (>10k rows) | Low | Stream responses using `IAsyncEnumerable`; add a configurable row limit (default 500) |
| App Insights API key stored in SQLite file is readable by any process running as the same OS user | Medium | Document that the `.db` file should be chmod 600 / stored in a user-only volume; encryption at rest is a v2 hardening task |
| Proxy masks the real error from the log source (e.g. rate limiting) | Low | Forward HTTP status codes and error bodies from the upstream source; do not swallow errors |

## Open questions
- If the project ever moves to a Tauri desktop app, this proxy architecture should be re-evaluated. The .NET layer could potentially be eliminated in favour of native HTTP calls from the Tauri backend.
