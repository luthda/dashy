# Plan: Phase 5 — Alerts & Real-time Notifications

> Design doc: `docs/design/phase-5-alerts.md`
> Mockups: `docs/mockups/dashy/` — no alerts-specific mockup; shell mockup covers sidebar changes
> Status: Draft

## Summary

Phase 5 adds passive alerting: alerts are polled every 60 s against a time-bounded App Insights window, firing events are pushed to the frontend via SSE, and the developer sees toast notifications and a bell dot without opening the logs page. The sequencing is: schema first → CRUD API → polling + SSE → frontend page → live stream.

## Phases

### Phase 0 — Database Migration

_Depends on: nothing. Additive nullable column — no backfill, zero downtime._

- [ ] `/backend-engineer` — Add `ResolvedAt DateTime?` to `Alert.cs`; map `resolved_at TEXT NULL` in `AlertConfiguration.cs`; generate EF Core migration `AddResolvedAtToAlerts` via `dotnet ef migrations add`

### Phase 1 — Alert CRUD API

_Depends on: Phase 0 (entity must have `ResolvedAt` before writing service)_

- [ ] `/backend-engineer` — Implement `AlertService.cs` (`Application/Alerts/`):
  - `GetAllAsync` — returns all alerts with `Source` navigation loaded, ordered by `CreatedAt`
  - `CreateAsync` — validates `checkIntervalSeconds ≥ 60`; if `tagId` present calls `BuildQueryFromTag` (see note below) and rejects Loki sources with HTTP 422; stores result in `query`
  - `UpdateAsync`, `DeleteAsync`
  - `ResolveAsync` — sets `status = Ok`, `resolvedAt = UtcNow`
  - `GetFiringsAsync` — newest-first, limit 50
  - **`BuildQueryFromTag(Tag, Source)`** — calls `AppInsightsAdapter.BuildKql(freeText: null, timeRange: null, tags: deserialized TagFilters, limit: 1000)` and trims the trailing `| limit 1000` line; the stored string is the base KQL without time range or count. The polling service appends ` | where timestamp >= datetime(…) and timestamp <= datetime(…) | count` at execution time.
  - **Query execution in polling** — `AlertPollingService` calls the App Insights HTTP API directly (bypassing `LogQueryService`) with a count-query derived from `alert.Query + time-range + | count`. User-entered "query mode" queries are treated as the raw base KQL fragment (same as tag-resolved queries).

- [ ] `/backend-engineer` — Implement `AlertEndpoints.cs` (`Controllers/`): map all CRUD routes + `POST /{id}/resolve` + `GET /{id}/firings`; register `AlertService` (scoped) and route group `/api/v1/alerts` in `Program.cs`

- [ ] `/backend-engineer` — Integration tests in `Dashy.Api.Tests`: create/list/update/delete/resolve cycle; verify `resolved_at` is set on resolve and cleared on re-fire; verify HTTP 422 for Loki source

### Phase 2 — Background Polling + SSE

_Depends on: Phase 1 (polling needs `AlertService` and the App Insights adapter)_

- [ ] `/backend-engineer` — Implement `AlertSseService.cs` (`Application/Alerts/`) as a **singleton**:
  - `ConcurrentDictionary<string, HttpResponse>` keyed by `connectionId` (new `Guid` per connection)
  - `AddClient` / `RemoveClient`
  - `BroadcastAsync(AlertFiredEvent)` — writes `event: alert-fired\ndata: {…}\n\n` to each registered response; catches `IOException` and removes stale entries

- [ ] `/backend-engineer` — Implement `AlertPollingService.cs` (`Application/Alerts/`) as a **`BackgroundService`**:
  - Uses `IServiceScopeFactory` to resolve scoped DB context per tick (BackgroundService is singleton-lifetime)
  - `PeriodicTimer(60 s)` loop; loads enabled due alerts
  - Per alert: sets `windowStart = utcNow - checkIntervalSeconds`, `windowEnd = utcNow`; executes count-query against App Insights; updates `status` / inserts `AlertFiring` / calls `AlertSseService.BroadcastAsync`; on exception → `status = Error`
  - On re-fire: clears `resolvedAt = null`; on clean poll: sets `status = Ok` (does **not** touch `resolvedAt`)

- [ ] `/backend-engineer` — Add SSE stream endpoint to `AlertEndpoints.cs` (`GET /alerts/stream`):
  - Sets `Content-Type: text/event-stream`, `Cache-Control: no-cache`, `X-Accel-Buffering: no`
  - Writes `retry: 3000\n\n`; registers client via `AlertSseService`
  - Starts `PeriodicTimer(30 s)` loop writing `: ping\n\n` as keep-alive heartbeat
  - Cancels on `HttpContext.RequestAborted`; removes client on disconnect
  - Register `AlertSseService` (singleton) and `AlertPollingService` (hosted service) in `Program.cs`

- [ ] `/backend-engineer` — Unit tests for `AlertPollingService` poll logic: firing transition, ok transition, error transition, `resolvedAt` cleared on re-fire, `resolvedAt` untouched on clean poll

### Phase 3 — Frontend Alerts Page

_Depends on: Phase 1 (needs CRUD API). Phase 2 not required — bell dot and list work via polling._

- [ ] `/frontend-engineer` — Add `Alert`, `AlertFiring`, `AlertStatus` types to `lib/types.ts`; implement hooks in `hooks/useAlerts.ts`: `useAlertsQuery`, `useCreateAlert`, `useUpdateAlert`, `useDeleteAlert`, `useResolveAlert`, `useAlertFiringsQuery` — follow the pattern in `hooks/useTags.ts`

- [ ] `/frontend-engineer` — Build `AlertList.tsx` (`components/alerts/`): table rows with columns name / source / status badge / check interval / last checked / actions; status badges: `ok` → green, `firing` → red, `error` → amber; "Resolved" button visible on `firing` rows only, calls `useResolveAlert`

- [ ] `/frontend-engineer` — Build `AlertFiringHistory.tsx` (`components/alerts/`): read-only two-column table (timestamp | result count), sourced from `useAlertFiringsQuery`; max 50 entries returned by API; no actions

- [ ] `/frontend-engineer` — Build `AlertSlideover.tsx` (`components/alerts/`): slide-out panel using shadcn `Sheet`; upper section = create/edit form (react-hook-form + zod schema from design doc); mode toggle "Query" / "From tag" — tag mode swaps query textarea for a tag select populated from `useTagsQuery`; lower section = `AlertFiringHistory` when editing an existing alert; "Delete" button with confirm on edit; on save calls `useCreateAlert` or `useUpdateAlert`

- [ ] `/frontend-engineer` — Build `AlertsPage.tsx` (`pages/`): route `/alerts`; "+ Create" button opens `AlertSlideover` in create mode; clicking a row opens it in edit mode; empty state: "No alerts yet — create one to start monitoring"

- [ ] `/frontend-engineer` — Wire routing and navigation: add `/alerts` route to `App.tsx`; add "Alerts" `NavLink` entry with `BellIcon` to `Sidebar.tsx` (mockup: `shell.jsx#Sidebar`); add orange dot indicator — `alerts.some(a => a.status === "Firing")` from `useAlertsQuery` (hook already fetched in sidebar for the dot; bell click navigates to `/alerts`)

### Phase 4 — Live SSE Notifications

_Depends on: Phase 2 (stream endpoint) + Phase 3 (AppShell with sidebar mounted)_

- [ ] `/frontend-engineer` — Implement `useAlertStream.ts` (`hooks/`): opens `EventSource("/api/v1/alerts/stream")`; on `alert-fired` event: shows shadcn toast "Alert: [name] fired — N results" (auto-dismiss 8 s); debounces per `alertId` — 1 toast per 60 s using a `useRef<Map<string, number>>`; after toast calls `queryClient.invalidateQueries({ queryKey: ["alerts"] })` to refresh list + bell dot; closes `EventSource` on cleanup

- [ ] `/frontend-engineer` — Mount `useAlertStream` once in `AppShell.tsx` so it runs across all pages; smoke-test: trigger a firing manually in the DB, verify toast appears and bell dot updates without a page refresh

---

## Dependency map

```
Phase 0 (migration)
  └─ Phase 1 (CRUD API) ──────────────┐
       └─ Phase 2 (polling + SSE)     │
            └─ Phase 4 (SSE hook)     │
                                      ▼
                              Phase 3 (frontend page)
                                      │
                                      └─ Phase 4 (mount in AppShell)
```

Non-obvious cross-phase dependencies:
- Phase 3 task "types + hooks" must land before all other Phase 3 tasks (all components import from it)
- Phase 3 sidebar bell dot task reads `useAlertsQuery` — that hook must exist first
- `AlertPollingService` (Phase 2) calls the App Insights HTTP API directly via the adapter, not through `LogQueryService` — it needs `AppInsightsAdapter` injected or its config/httpclient accessible; backend engineer should resolve this during implementation (options: inject `ILogSourceAdapterFactory` and call `adapter.QueryAsync` with a pre-built count-query, or execute the App Insights REST API directly)

## Open questions

_All design-level questions resolved. One implementation-level decision remains:_

| # | Question | Blocks |
|---|---|---|
| 1 | How does `AlertPollingService` execute the stored `alert.Query` as a count query? See the note in Phase 1 `AlertService` task. Options: (a) inject `ILogSourceAdapterFactory`, pass query as `FreeText` to `AdapterQueryRequest` with a time range, use result count; (b) call App Insights REST directly with the stored KQL + appended count clause. Option (a) is simpler but treats stored query as free-text; option (b) allows raw KQL storage but requires direct HTTP call. | Phase 2 `AlertPollingService` |
