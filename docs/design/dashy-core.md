# Dashy — Core Design Doc

_Date: 2026-06-04_

---

## Problem Statement

**Context:** A developer debugs services that emit logs to one or more sources (Azure App Insights, Loki/Grafana). When an issue occurs they must navigate to the source portal, rebuild their query from scratch each session, and tolerate a UI not designed for fast, repeated debugging workflows.

**Problem:** There is no single place to define log queries once, save them, and return to them instantly — forcing the developer to waste time on portal navigation and query reconstruction rather than on the actual problem.

**Success criteria:**
- Connect a log source (Azure App Insights or Loki) in under 2 minutes
- Saved searches are persisted and executable with one click
- Tags can be defined from EventType / log level / search terms and applied as chips in the log view
- Alerts fire an in-dashboard notification (toast + history panel) when matching logs appear
- The developer never needs to open Azure Portal or Grafana for routine log investigation

**Out of scope (v1):** Multi-user sharing, metrics data (UI shell only), traces (UI shell only), live log streaming, email/Slack alert delivery, cross-source merged search results

---

## Functional Spec

### User stories
- As a developer, I can connect an Azure App Insights or Loki source so that Dashy can query my logs
- As a developer, I can search logs with free text, EventType, and log level filters so that I find relevant events quickly
- As a developer, I can define a tag from a combination of search term / log level / EventType so that I can reuse filter combinations as chips
- As a developer, I can save a search (query + time range + refresh interval) so that I can replay the exact same investigation with one click
- As a developer, I can configure an alert with its own query so that I receive an in-dashboard notification when matching logs appear
- As a developer, I can see a history of past alert firings so that I know when and how often conditions triggered

### User journeys

#### First run
1. User opens Dashy → empty dashboard with "Connect a source" prompt
2. Clicks prompt → source config panel: type selector (App Insights / Loki), credential fields (App ID + API key for App Insights; base URL + optional auth token for Loki)
3. Source saved, connection tested → redirect to Logs page for that source

#### Log investigation
1. User lands on Logs page
   - Search bar (free text)
   - Time span picker (relative: last 15m / 1h / 6h / 24h / 7d — and absolute range)
   - Refresh button + Live toggle button
   - Tag chips row (applied tags shown as removable chips)
2. User types a query, selects a time range, clicks Refresh
3. Results appear:
   - Bar chart: log counts bucketed by time, colour-coded by level
   - Log table: timestamp, level, message, source, properties (expandable row)
4. User clicks a tag chip → its filters are ANDed into the active query, results refresh

#### Tag management
1. User opens Tags menu (accessible from Logs page header)
2. Creates a tag: name, one or more of: free-text term, EventType, log level
3. Tag saved → appears as a chip in the chip row on the Logs page
4. A tag chip can be applied/removed without navigating away

#### Saved search
1. User sets a query + time range + auto-refresh interval (e.g. every 30 s)
2. Clicks "Save Search" → names it (e.g. "Winfap exceptions – last 24 h")
3. Saved searches listed in a drawer or sidebar panel
4. Clicking a saved search restores the full query/time/interval state instantly

#### Alert
1. User opens Alerts (sidebar nav item)
2. Creates an alert: name, source, query, check interval (e.g. every 5 min), threshold (≥ N results triggers)
3. Alert enabled → .NET background service polls on the defined interval
4. Alert fires:
   - Toast notification appears (auto-dismisses after 8 s)
   - Alert bell icon gains a badge count
5. User clicks alert bell → alert history panel opens, showing timestamped list of all firings

### Permissions
Single-user application. All configuration (sources, tags, saved searches, alerts) belongs to the local installation.

### Edge cases & failure states

| Scenario | Behaviour |
|---|---|
| Source unreachable / bad credentials | Inline error banner on Logs page; alert polling suspends with `error` state shown in alert list |
| Query returns 0 results | Empty state with suggestion to widen the time range |
| Saved search's source was deleted | Saved search marked `broken`; prompt to re-link or delete |
| Alert fires repeatedly in a short window | Toasts debounced (max 1 toast per alert per minute); all firings still written to history |
| Live mode + expensive query | Warning if estimated query cost / rate exceeds a configurable threshold |
| Invalid query syntax | Inline syntax error beneath the search bar before the query is sent |

### Out of scope
Metrics page (UI shell, no data), Traces page (UI shell, no data), live log streaming, multi-user/org support, email/Slack notifications, cross-source merged queries

---

## Technical Spec

### Architecture overview

```
┌──────────────────────────────┐
│  React Vite SPA (shadcn/ui)  │  Port 5173 (dev)
└────────────┬─────────────────┘
             │ REST / JSON
┌────────────▼─────────────────┐
│  .NET 10 Web API             │  Port 8080
│  ├─ Query proxy controller   │  Calls App Insights / Loki (bypasses CORS)
│  ├─ Config API (CRUD)        │  Sources, tags, saved searches, alerts
│  └─ Alert background service │  IHostedService, polls on schedule
└────────────┬─────────────────┘
             │ EF Core
┌────────────▼─────────────────┐
│  SQLite (file, Docker volume)│
└──────────────────────────────┘
```

The .NET API proxies all log source calls primarily to bypass **CORS** — neither `api.applicationinsights.io` nor a typical Loki deployment accepts browser-origin requests. A secondary benefit is that API keys are held server-side and never appear in browser network traffic. See ADR-002.

The database is SQLite (not PostgreSQL) — a single `.db` file mounted as a Docker volume. No Postgres container is needed. See ADR-001.

---

### Data model

#### `sources`
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| name | text | Display name |
| type | enum | `app_insights`, `loki` |
| config | jsonb | Encrypted: connection details per type |
| created_at | timestamptz | |

App Insights config shape: `{ appId, apiKey }`
Loki config shape: `{ baseUrl, orgId?, authToken? }`

#### `tags`
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| name | text | |
| color | text | Hex, for chip UI |
| filters | jsonb | `{ terms: string[], levels: string[], eventTypes: string[] }` |
| created_at | timestamptz | |

#### `saved_searches`
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| name | text | |
| source_id | uuid FK → sources | |
| query | text | Raw query string |
| tag_ids | uuid[] | Applied tag IDs |
| time_range | jsonb | `{ type: 'relative'\|'absolute', value: string, from?: ts, to?: ts }` |
| refresh_interval_seconds | int | null = manual refresh |
| created_at | timestamptz | |

#### `alerts`
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| name | text | |
| source_id | uuid FK → sources | |
| query | text | |
| check_interval_seconds | int | |
| threshold | int | Fire when result count ≥ this |
| enabled | bool | |
| last_checked_at | timestamptz | |
| status | enum | `ok`, `firing`, `error` |
| created_at | timestamptz | |

#### `alert_firings`
| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| alert_id | uuid FK → alerts | |
| fired_at | timestamptz | |
| result_count | int | Count that triggered the alert |

---

### Log normalisation model

The .NET backend maps both App Insights and Loki responses to a shared `LogEntry`:

```csharp
record LogEntry(
    DateTimeOffset Timestamp,
    string Level,        // "error" | "warn" | "info" | "debug" | "trace"
    string Message,
    string Source,       // source name
    string? EventType,   // maps to App Insights customDimensions.EventType or Loki label
    Dictionary<string, string> Properties  // remaining fields
);
```

Mapping notes:
- **App Insights** (`traces` / `exceptions` table via REST API): `severityLevel` → `Level`, `message` → `Message`, all `customDimensions` entries → `Properties`
- **Loki** (HTTP query API, LogQL): stream labels → `Properties`, structured metadata field `level` → `Level`

---

### API design

All routes under `/api/v1/`. Request/response bodies are JSON.

#### Sources
| Method | Path | Description |
|---|---|---|
| GET | `/sources` | List all sources |
| POST | `/sources` | Create source |
| PUT | `/sources/{id}` | Update source |
| DELETE | `/sources/{id}` | Delete source |
| POST | `/sources/{id}/test` | Test connection, returns `{ ok: bool, error?: string }` |

#### Logs
| Method | Path | Description |
|---|---|---|
| POST | `/logs/query` | Execute a log query, returns `LogEntry[]` |

Request body:
```json
{
  "sourceId": "uuid",
  "query": "winfap",
  "tagIds": ["uuid"],
  "timeRange": { "type": "relative", "value": "1h" },
  "limit": 500
}
```

#### Tags
| Method | Path | Description |
|---|---|---|
| GET | `/tags` | List all tags |
| POST | `/tags` | Create tag |
| PUT | `/tags/{id}` | Update tag |
| DELETE | `/tags/{id}` | Delete tag |

#### Saved searches
| Method | Path | Description |
|---|---|---|
| GET | `/saved-searches` | List |
| POST | `/saved-searches` | Create |
| PUT | `/saved-searches/{id}` | Update |
| DELETE | `/saved-searches/{id}` | Delete |

#### Alerts
| Method | Path | Description |
|---|---|---|
| GET | `/alerts` | List alerts (includes status) |
| POST | `/alerts` | Create alert |
| PUT | `/alerts/{id}` | Update alert |
| DELETE | `/alerts/{id}` | Delete alert |
| GET | `/alerts/{id}/firings` | Get firing history |

#### Real-time alert push
When the alert background service detects a firing, it broadcasts a server-sent event (SSE) on `/api/v1/alerts/stream`. The React SPA subscribes to this stream and shows the toast notification without polling.

---

### Frontend architecture

```
src/
  components/
    ui/               # shadcn/ui primitives (auto-generated, do not edit)
    logs/
      LogsPage.tsx
      SearchBar.tsx
      TagChipRow.tsx
      LogTable.tsx
      LogLevelChart.tsx
    tags/
      TagsDialog.tsx
    saved-searches/
      SavedSearchDrawer.tsx
    alerts/
      AlertsPage.tsx
      AlertHistoryPanel.tsx
      AlertToast.tsx        # subscribes to SSE stream
    sources/
      SourceSetupDialog.tsx
    layout/
      AppShell.tsx          # sidebar nav: Logs / Metrics / Traces / Alerts
  hooks/
    useLogQuery.ts          # wraps POST /logs/query, handles loading/error state
    useAlertStream.ts       # opens SSE connection, dispatches toast events
  lib/
    api.ts                  # typed fetch wrappers
```

**State management:** React Query (TanStack Query) for server state; no global client state store needed for v1.

**Routing:** React Router v6. Routes: `/` → redirect to `/logs`, `/logs`, `/metrics` (shell), `/traces` (shell), `/alerts`, `/settings/sources`.

**Tag chips:** Rendered with shadcn `Badge` in a wrapping flex row. Clicking a chip toggles it; active chips are passed as `tagIds` in the query payload.

---

### Alert background service

`AlertPollingService : IHostedService` runs a `PeriodicTimer`-based loop:

1. Load all enabled alerts from DB
2. For each alert due a check (based on `last_checked_at + check_interval_seconds`):
   a. Execute query against source via the same proxy logic as the API
   b. If `resultCount >= threshold`: insert `alert_firings` row, update `alerts.status = firing`, broadcast SSE event
   c. Else: update `alerts.status = ok`
   d. Update `alerts.last_checked_at`
3. Sleep until the next alert is due

SSE is implemented with `IServerSentEventsService` (or a simple `ConcurrentDictionary` of response streams if not using a library).

---

### Rollout
- Single Docker Compose file: `postgres`, `dashy-api` (.NET), `dashy-web` (Nginx serving the Vite build)
- Environment variables for secrets: `ENCRYPTION_KEY`, `POSTGRES_CONNECTION_STRING`
- No feature flags needed for v1; Metrics and Traces pages are UI shells (empty state with "Coming soon")

---

### Open questions

| # | Question | Owner | Priority |
|---|---|---|---|
| 1 | App Insights uses a REST API (`api.applicationinsights.io/v1/apps/{appId}/query`) with an `X-API-Key` header. Loki uses LogQL over HTTP. Do we need to support Loki with BasicAuth or just a Bearer token? | Kevin | High — needed before Loki source is built |
| 2 | What is the maximum number of log rows to return per query? (affects UX pagination vs. virtual scroll) | Kevin | Medium |
| 3 | Should tag filter combinations be ANDed or ORed when multiple chips are active? | Kevin | High — affects query construction |
| 4 | Credential encryption: use AES-256-GCM with a key from env var, or integrate .NET Data Protection API? | Engineering | Medium |
| 5 | Live mode: deferred to v2 — confirm the approach (SSE stream from .NET, or WebSocket, or short-poll) before starting | Kevin | Low (v2) |
