# Plan: Dashy Core

> Design doc: `docs/design/dashy-core.md`
> Mockups: `docs/mockups/dashy/`
> Status: Phase 0–2 complete

## Summary

Dashy Core delivers the end-to-end log investigation workflow: connect a source, query logs, apply tag filters, save searches, and receive alert notifications. The plan sequences backend-first per domain (sources → logs → tags → saved searches → alerts) so that each phase produces a working vertical slice — the frontend builds against real endpoints, and every phase boundary is deployable.

---

## Phases

### Phase 0 — Project Scaffolding & Infrastructure

Bootstrap both projects, Docker Compose, and the database schema foundation.
_Depends on: nothing_

- [x] `/backend-engineer` — Scaffold .NET 10 Web API project (`src/Dashy.Api/`): `Program.cs` with minimal API setup, EF Core + Npgsql registration, global exception handler (`IExceptionHandler` → `ProblemDetails`), options pattern for `DatabaseOptions` and `EncryptionOptions`
- [x] `/backend-engineer` — Scaffold test project (`src/Dashy.Api.Tests/`): xUnit, `DashyWebApplicationFactory` with Testcontainers PostgreSQL, FluentAssertions
- [x] `/frontend-engineer` — Scaffold React Vite project (`src/dashy-web/`): TypeScript strict, Tailwind CSS v4, shadcn/ui init, React Router v7, TanStack Query v5 provider, `lib/api.ts` typed fetch client
- [x] `/backend-engineer` — Create Docker Compose file: `postgres` (v17), `dashy-api` (.NET), `dashy-web` (Nginx), shared network, volume for PostgreSQL data
- [x] `/backend-engineer` — Implement `IEncryptionService` (AES-256-GCM, key from `ENCRYPTION_KEY` env var) with unit tests

### Phase 1 — Sources

Connect a log source, test the connection, manage sources. Enables the first-run journey.
_Depends on: Phase 0_

- [x] `/backend-engineer` — EF Core migration `CreateSourcesTable`: `sources` table with UUID PK, `name`, `type` (enum → string), `config` (text, encrypted), `created_at` (default `now()`)
- [x] `/backend-engineer` — `Source` entity, `SourceConfiguration`, `SourceService` (CRUD + encrypt/decrypt config), `SourceEndpoints` (GET, POST, PUT, DELETE `/api/v1/sources`)
- [x] `/backend-engineer` — App Insights query client: HTTP client calling `api.applicationinsights.io/v1/apps/{appId}/query`, maps response to `LogEntry[]`
- [x] `/backend-engineer` — Loki query client: HTTP client calling Loki HTTP API with LogQL, maps response to `LogEntry[]`
- [x] `/backend-engineer` — `POST /api/v1/sources/{id}/test` endpoint: decrypts config, calls the source, returns `{ ok, error? }`
- [x] `/backend-engineer` — Integration tests for source CRUD and connection test endpoints
- [x] `/frontend-engineer` — AppShell layout with collapsible sidebar and topbar (mockup: `shell.jsx#Sidebar`, `shell.jsx#Topbar`) — nav items: Logs, Metrics (shell), Traces (shell), Alerts, Settings
- [x] `/frontend-engineer` — React Router setup: `/` → redirect to `/logs`, lazy-loaded page routes
- [x] `/frontend-engineer` — Source query/mutation hooks: `useSourcesQuery`, `useCreateSource`, `useUpdateSource`, `useDeleteSource`, `useTestConnection`
- [x] `/frontend-engineer` — SourceSetupDialog with react-hook-form + zod: type selector (App Insights / Loki), conditional credential fields, test connection button (mockup: `data.jsx#SOURCES` for source types)
- [x] `/frontend-engineer` — Settings page: source list with status, add/edit/delete actions
- [x] `/frontend-engineer` — First-run empty state on Logs page: "Connect a source" prompt linking to SourceSetupDialog

### Phase 2 — Log Query & Display

Query logs through the API proxy and display results. The core investigation workflow.
_Depends on: Phase 1_

- [x] `/backend-engineer` — `LogEntry` record, `LogQueryService` (dispatches to App Insights or Loki client based on source type), `POST /api/v1/logs/query` endpoint accepting `{ sourceId, query, tagIds, timeRange, limit }`
- [x] `/backend-engineer` — Log normalisation: App Insights `severityLevel` → level, `customDimensions` → properties; Loki stream labels → properties, `level` label → level
- [x] `/backend-engineer` — Integration tests for log query endpoint (mock external HTTP via `IHttpClientFactory`)
- [x] `/frontend-engineer` — `useLogQuery` hook wrapping `POST /logs/query` with TanStack Query (enabled only when params are set)
- [x] `/frontend-engineer` — LogsPage layout: search bar, time range picker (15m / 1h / 6h / 24h / 7d), refresh button, live toggle placeholder (mockup: `logs.jsx#LogsPage` controls section)
- [x] `/frontend-engineer` — Stacked severity histogram: bar chart of log counts bucketed by time, colour-coded by level (mockup: `charts.jsx#StackedHistogram`)
- [x] `/frontend-engineer` — Level filter chips: toggle individual severity levels to filter displayed results (mockup: `logs.jsx` level toggle buttons)
- [x] `/frontend-engineer` — LogTable with expandable rows: timestamp, level badge (colour-coded), message, source; expanded view shows eventType + properties key-value grid (mockup: `logs.jsx#LogStream`, `logs.jsx#LogLine`)
- [x] `/frontend-engineer` — Skeleton loading state for table (8 rows), empty state ("No results — try widening the time range"), inline error banner for source errors

### Phase 3 — Tags

Define reusable filter combinations as tag chips. Enhances the log investigation workflow.
_Depends on: Phase 2_

> **Architecture note (ADR-004):** Tag entity lives in `Domain/Entities/`, service in
> `Application/Services/`, endpoints in `Controllers/`, EF config in
> `Infrastructure/Persistence/Configurations/`. Services use `DashyDbContext` directly
> (no repository abstraction).

- [x] `/backend-engineer` — `tags` table included in `InitialSchema` migration; `Tag` entity (`Domain/Entities/Tag.cs`), `TagConfiguration` (`Infrastructure/Persistence/Configurations/TagConfiguration.cs`)
- [x] `/backend-engineer` — Tag filter integration in `LogQueryService.ResolveTagFiltersAsync` — resolves `tagIds` → merges terms/levels/eventTypes into the adapter query
- [x] `/backend-engineer` — `TagService` (CRUD) in `Application/Services/TagService.cs`, `TagEndpoints` in `Controllers/TagEndpoints.cs` (GET, POST, PUT, DELETE `/api/v1/tags`), wired in `Program.cs`
- [x] `/backend-engineer` — Integration tests for tag CRUD endpoints (`Integration/TagEndpointTests.cs`)
- [x] `/frontend-engineer` — Tag query/mutation hooks: `useTagsQuery`, `useCreateTag`, `useUpdateTag`, `useDeleteTag`
- [x] `/frontend-engineer` — TagsDialog: create/edit form with name, colour picker, multi-select for terms/levels/eventTypes (react-hook-form + zod)
- [x] `/frontend-engineer` — TagChipRow on Logs page: rendered as shadcn `Badge` components, click to toggle, active tags passed as `tagIds` in query (mockup: `logs.jsx` search bar area with tag chips like `env : prod`)
- [x] `/frontend-engineer` — Tags menu accessible from Logs page header

### Phase 4 — Saved Searches

Save and restore full query state with one click.
_Depends on: Phase 3_

- [ ] `/backend-engineer` — EF Core migration `CreateSavedSearchesTable`: `saved_searches` table with UUID PK, `name`, `source_id` FK, `query`, `tag_ids` (UUID array), `time_range` (JSONB), `refresh_interval_seconds`, `created_at`
- [ ] `/backend-engineer` — `SavedSearch` entity, `SavedSearchConfiguration`, `SavedSearchService` (CRUD + mark broken when source deleted), `SavedSearchEndpoints` (GET, POST, PUT, DELETE `/api/v1/saved-searches`)
- [ ] `/backend-engineer` — Integration tests for saved search CRUD
- [ ] `/frontend-engineer` — Saved search query/mutation hooks: `useSavedSearchesQuery`, `useCreateSavedSearch`, `useUpdateSavedSearch`, `useDeleteSavedSearch`
- [ ] `/frontend-engineer` — SavedSearchDrawer: list of saved searches in a slide-out drawer, click to restore full query/time/tag state to LogsPage
- [ ] `/frontend-engineer` — "Save Search" button on LogsPage: opens dialog to name the current query state
- [ ] `/frontend-engineer` — Auto-refresh support: when a saved search has `refreshIntervalSeconds`, pass `refetchInterval` to `useLogQuery`
- [ ] `/frontend-engineer` — Broken saved search indicator: show warning when source was deleted, prompt to re-link or delete

### Phase 5 — Alerts & Real-time Notifications

Alert background polling, SSE push, and the alerts management page.
_Depends on: Phase 2_ (tags/saved searches are independent — alerts only need log query)

- [ ] `/backend-engineer` — EF Core migration `CreateAlertsAndFiringsTable`: `alerts` table (UUID PK, `name`, `source_id` FK, `query`, `check_interval_seconds`, `threshold`, `enabled`, `last_checked_at`, `status` enum, `created_at`) + `alert_firings` table (UUID PK, `alert_id` FK, `fired_at`, `result_count`)
- [ ] `/backend-engineer` — `Alert` + `AlertFiring` entities, configurations, `AlertService` (CRUD), `AlertEndpoints` (GET, POST, PUT, DELETE `/api/v1/alerts`, GET `/api/v1/alerts/{id}/firings`)
- [ ] `/backend-engineer` — `AlertSseService` singleton: manages `ConcurrentDictionary<string, StreamWriter>` of SSE clients, `BroadcastAsync` method, `GET /api/v1/alerts/stream` endpoint
- [ ] `/backend-engineer` — `AlertPollingService` (`BackgroundService` + `PeriodicTimer`): loads due alerts, executes queries via `LogQueryService`, creates firings, updates status, broadcasts SSE events
- [ ] `/backend-engineer` — Integration tests for alert CRUD, firing history, and polling service logic
- [ ] `/frontend-engineer` — Alert query/mutation hooks: `useAlertsQuery`, `useCreateAlert`, `useUpdateAlert`, `useDeleteAlert`, `useAlertFiringsQuery`
- [ ] `/frontend-engineer` — AlertsPage: alert list with status badges (ok/firing/error), create/edit form (react-hook-form + zod: name, source, query, interval, threshold, enabled toggle)
- [ ] `/frontend-engineer` — AlertHistoryPanel: firing history list for a selected alert (timestamp + result count)
- [ ] `/frontend-engineer` — `useAlertStream` hook: opens `EventSource` to `/api/v1/alerts/stream`, dispatches toasts (8s auto-dismiss, debounced 1 per alert per minute), invalidates alert queries
- [ ] `/frontend-engineer` — Alert bell icon in sidebar/topbar with badge count, click opens alert history (mockup: `data.jsx#ICONS.bell`)

### Phase 6 — Shell Pages & Polish

Placeholder pages for future features, Docker finalisation, end-to-end smoke test.
_Depends on: Phase 5_

- [ ] `/frontend-engineer` — Metrics page shell: "Coming soon" empty state (mockup: `app.jsx#Placeholder`)
- [ ] `/frontend-engineer` — Traces page shell: "Coming soon" empty state
- [ ] `/frontend-engineer` — Dark/light theme toggle in topbar (mockup: `shell.jsx#Topbar` with `app.jsx` theme toggle)
- [ ] `/backend-engineer` — Vite proxy config for dev (port 5173 → 8080) and Nginx config for production
- [ ] `/backend-engineer` — Docker Compose smoke test: build all containers, verify end-to-end source → query → results flow

---

## Dependency Map

```
Phase 0 (scaffolding)
  └─► Phase 1 (sources)
        └─► Phase 2 (log query)
              ├─► Phase 3 (tags) ─────────► Phase 4 (saved searches)
              └─► Phase 5 (alerts & SSE)
                                             └─► Phase 6 (polish)
```

- **Phase 3 and 5 are independent** of each other and can run in parallel after Phase 2.
- **Phase 4 depends on Phase 3** because saved searches include `tagIds`.
- **Phase 6** is a catch-all and depends on all prior phases being complete.
- The App Insights and Loki query clients (Phase 1) are required before any log query work (Phase 2).

---

## Open Questions

| # | Question | From | Blocks |
|---|---|---|---|
| 1 | Loki auth: BasicAuth or Bearer token only? | Design doc Q1 | Phase 1 — Loki query client |
| 2 | Max log rows per query? (affects pagination vs. virtual scroll) | Design doc Q2 | Phase 2 — LogTable (virtual scroll if > 500) |
| 3 | ~~Tag filter combination: AND or OR when multiple chips active?~~ **Resolved: OR.** Multiple active tags union their filters — wider selection, not narrower. Already implemented in `ResolveTagFiltersAsync`. | Design doc Q3 | ~~Phase 3~~ Done |
| 4 | Design doc architecture says SQLite; rollout section and backend-engineer skill say PostgreSQL. Which is canonical? | Plan author | Phase 0 — DB setup. Plan assumes PostgreSQL per backend-engineer skill |
| 5 | Credential encryption: AES-256-GCM (env var key) vs. .NET Data Protection API? | Design doc Q4 | Phase 0 — IEncryptionService. Plan assumes AES-256-GCM per backend-engineer skill |
