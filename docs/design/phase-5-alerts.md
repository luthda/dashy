# Phase 5 — Alerts & Real-time Notifications

_Date: 2026-06-10_

> **Revision (2026-06-10, PR #18 review):** the per-alert check interval
> (`check_interval_seconds`) was removed. Every enabled alert is checked on the
> standard 60-second polling tick — the same cadence as the frontend's live
> mode. The poll window is `(last_checked_at, now]` filtered on
> **`ingestion_time()`** (not `timestamp`), so App Insights ingestion lag can no
> longer cause firings to be missed. Threshold, enabled flag, and all other
> behaviour are unchanged. References to a configurable interval below are
> historical.

---

## Problem Statement

**Context:** A solo developer runs services that emit logs to Azure App Insights or Loki. When a regression occurs they only find out reactively — waiting for a visible failure or periodically opening the Dashy logs page to check manually.

**Problem:** There is no passive safety net that monitors specific log conditions and notifies the developer proactively. Without alerts, the developer must actively watch the logs page or rely on external, disconnected alerting tools (Azure Monitor, Grafana) that aren't connected to their saved Dashy queries.

**Success criteria:**
- Developer defines an alert (query or tag + source + threshold + interval) once and receives a toast notification when the condition is met — without opening the logs page
- Bell icon in the sidebar shows an orange dot when any firing alerts are unresolved
- Clicking the bell navigates to the Alerts page
- Firing alerts remain visible until the developer clicks "Resolved"
- All firings are preserved in history regardless of resolution state

**Out of scope:** External delivery channels (email, Slack), multi-user shared alerts, complex conditions (absence of events, rate-of-change), per-firing acknowledgment, alert grouping or correlation across sources

---

## Functional Spec

### User stories
- As a developer, I can create an alert from a query or an existing tag so that I am notified passively when the condition is met
- As a developer, I can enable/disable an alert so that I can temporarily stop watching without deleting it
- As a developer, I can see all alerts with their current status (ok / firing / error) at a glance on the Alerts page
- As a developer, I receive a toast notification when an alert fires so I know without watching the logs page
- As a developer, I see an orange dot on the bell icon when any unresolved firing alerts exist
- As a developer, clicking the bell navigates me to the Alerts page
- As a developer, I can click "Resolved" to clear a firing alert so the orange dot disappears
- As a developer, I can view the read-only firing history (timestamp + result count) for any alert

### User journeys

#### Create alert — from query
1. Sidebar → Alerts → empty state or alert list
2. Click "+ Create" → slide-out panel opens
3. Choose mode: "Query" (default) or "From tag"
4. Query mode form: name, source (select), query (textarea), check interval (select: 1 min / 5 min / 15 min / 30 min / 60 min), threshold (≥ N results), enabled toggle
5. Save → alert added to list with status `ok`

#### Create alert — from tag
1. Same as above but choose "From tag" mode
2. Form: name, tag (select from existing tags), source (select — required because tags are source-agnostic), check interval, threshold, enabled toggle
3. On save, backend resolves the tag's filters (terms / levels / eventTypes) into the appropriate query string for the selected source type (KQL for App Insights, LogQL for Loki) and stores the result in `query`
4. After save, the alert is independent of the tag — editing the tag later does not change the alert

#### Alert fires
1. Background service polls; finds `result_count ≥ threshold`
2. Status → `firing`; `alert_firings` row inserted; `resolved_at` cleared
3. SSE event broadcast to all connected frontend clients
4. Frontend receives event:
   - Toast appears (auto-dismiss 8 s): "Alert: [name] fired — N results"
   - Bell icon gains orange dot (if not already present)
5. Alerts page shows `firing` status badge on the row

#### Resolve a firing alert
1. Developer opens Alerts page (via bell click or sidebar nav)
2. Sees the row with `firing` badge
3. Clicks "Resolved" → `status = ok`, `resolved_at = now()`
4. If no remaining `firing` alerts, orange bell dot clears
5. Alert resumes polling normally; will only re-fire when *new* log entries matching the query appear in a future poll window — the same old log entries that caused the original firing will not re-trigger it

#### View firing history
1. Click any alert row → slide-out panel opens
2. Edit form in upper section; read-only "Firing history" table below: `timestamp | result count`
3. No actions per row

#### Edit / delete
1. Click alert → slide-out opens with pre-filled form
2. Save to update; "Delete" → confirm → alert removed (cascade deletes firings)

### Alert status model

| Status | Meaning |
|---|---|
| `ok` | Last poll clean, or alert was just resolved, or never polled yet |
| `firing` | Last poll exceeded threshold; not yet resolved |
| `error` | Source deleted, source unreachable, or query execution failed |

### Permissions
Single-user — no permission model.

### Edge cases & failure states

| Scenario | Behaviour |
|---|---|
| Source deleted while alert exists | Alert → `error` on next poll attempt; error badge shown |
| Invalid query | Surfaces at first poll execution; status → `error` |
| Alert fires repeatedly (< 1 min apart) | Toasts debounced: 1 per alert per minute; all firings written to history |
| Resolved, next poll still above threshold (same old logs) | Does not re-fire — poll window covers only the last `check_interval_seconds`, so logs older than the window are not evaluated |
| Resolved, new logs appear above threshold in next window | Re-fires — new toast, orange dot returns |
| SSE connection drops | `EventSource` auto-reconnects; missed firings visible on Alerts page |
| No alerts configured | Empty state: "No alerts yet — create one to start monitoring" |
| Alert disabled mid-fire | Polling skips disabled alerts; status remains as-is until re-enabled |
| Minimum interval validation | UI and API enforce `check_interval_seconds ≥ 60` |

### Out of scope
Email/Slack delivery, firing-row-to-Logs navigation, complex conditions (absence, rate-of-change), per-firing acknowledgment

---

## Technical Spec

### Data model

The `alerts` and `alert_firings` tables are already created by `InitialSchema`. One additive migration is needed.

**New migration: `AddResolvedAtToAlerts`**

```sql
ALTER TABLE alerts ADD COLUMN resolved_at TEXT NULL;
```

**Updated `alerts` table (full shape after migration):**

| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| name | text | max 200 chars |
| source_id | uuid FK → sources | CASCADE delete |
| query | text | Raw KQL or LogQL; populated at save time (flattened from tag if needed) |
| check_interval_seconds | int | Default 300; min 60 enforced |
| threshold | int | Fire when result_count ≥ threshold; default 1 |
| enabled | bool | Default true |
| last_checked_at | timestamptz | Nullable |
| status | enum text | `Ok` / `Firing` / `Error` |
| resolved_at | timestamptz | Nullable; set by "Resolved" action, cleared when next firing occurs |
| created_at | timestamptz | Default now() |

**`alert_firings` table — no changes:**

| Column | Type | Notes |
|---|---|---|
| id | uuid PK | |
| alert_id | uuid FK → alerts | CASCADE delete |
| fired_at | timestamptz | |
| result_count | int | |

**Bell dot logic:** `resolved_at` is a convenience audit field. The authoritative signal for the bell dot is `alerts.status = Firing` for any row. The UI queries `GET /api/v1/alerts` and checks if any have `status: "Firing"`.

**Tag-to-query flattening:** Resolved server-side in `AlertService.BuildQueryFromTag(Tag tag, Source source)`. Uses the same translation logic as `LogQueryService` (KQL for `app_insights`, LogQL for `loki`). The resulting query string is stored in `alerts.query`; no `tag_id` column is needed.

### API design

All under `/api/v1/`. New endpoints only — existing routes unchanged.

#### Alerts CRUD

| Method | Path | Description |
|---|---|---|
| GET | `/alerts` | List all alerts (includes status, resolved_at) |
| POST | `/alerts` | Create alert |
| PUT | `/alerts/{id}` | Update alert |
| DELETE | `/alerts/{id}` | Delete alert (cascades firings) |
| POST | `/alerts/{id}/resolve` | Set status=Ok, resolved_at=now() |
| GET | `/alerts/{id}/firings` | Firing history, newest first, max 50 entries |

**POST `/alerts` request body:**

```json
{
  "name": "Winfap exceptions",
  "sourceId": "uuid",
  "query": "exceptions | where ...",
  "checkIntervalSeconds": 300,
  "threshold": 1,
  "enabled": true
}
```

Or for tag-based creation (resolved server-side before storage):

```json
{
  "name": "Winfap exceptions",
  "sourceId": "uuid",
  "tagId": "uuid",
  "checkIntervalSeconds": 300,
  "threshold": 1,
  "enabled": true
}
```

If `tagId` is present, the backend calls `BuildQueryFromTag` and stores the result in `query`; `tagId` is not persisted.

**GET `/alerts` response item:**

```json
{
  "id": "uuid",
  "name": "string",
  "sourceId": "uuid",
  "sourceName": "string",
  "query": "string",
  "checkIntervalSeconds": 300,
  "threshold": 1,
  "enabled": true,
  "lastCheckedAt": "2026-06-10T...",
  "status": "Ok|Firing|Error",
  "resolvedAt": "2026-06-10T...|null",
  "createdAt": "2026-06-10T..."
}
```

#### SSE stream

| Method | Path | Description |
|---|---|---|
| GET | `/alerts/stream` | Long-lived SSE connection |

Event format (text/event-stream):

```
event: alert-fired
data: {"alertId":"uuid","alertName":"Winfap exceptions","resultCount":5,"firedAt":"2026-06-10T12:00:00Z"}
```

### Backend architecture

**New files:**

```
Dashy.Api/
  Application/
    Alerts/
      AlertService.cs          # CRUD + BuildQueryFromTag + Resolve
      AlertSseService.cs       # Singleton; ConcurrentDictionary<string, HttpResponse>
      AlertPollingService.cs   # BackgroundService + PeriodicTimer(60s)
  Api/
    Endpoints/
      AlertEndpoints.cs        # Maps all /api/v1/alerts routes
```

**`AlertSseService` (singleton):**
- Holds `ConcurrentDictionary<string, HttpResponse>` keyed by a `connectionId` (new Guid per connection)
- `AddClient(id, response)` / `RemoveClient(id)` called by the stream endpoint
- `BroadcastAsync(AlertFiredEvent)` — writes `event: alert-fired\ndata: {...}\n\n` to all registered responses; removes stale entries on `IOException`
- Stream endpoint: writes `retry: 3000\n\n` header, registers client, starts a 30 s heartbeat loop (`PeriodicTimer(30s)`) writing `: ping\n\n` to keep proxies alive, awaits `HttpContext.RequestAborted`, removes client on disconnect

**`AlertPollingService` (BackgroundService):**

```
Loop every 60s:
  Load all enabled alerts
  For each alert:
    windowStart = last_checked_at ?? now - 60s   (windows tile across polls)
    windowEnd   = now
    Try: execute count query via the source adapter, filtered on
         ingestion_time() in (windowStart, windowEnd]
    If resultCount >= threshold:
      Insert AlertFiring(alertId, firedAt=now, resultCount)
      Update alert: status=Firing, resolved_at=null, last_checked_at=now
      BroadcastAsync via AlertSseService
    Else:
      Update alert: status=Ok, last_checked_at=now
      (do NOT touch resolved_at here — only cleared on re-fire above)
    On exception (source gone, query error):
      Update alert: status=Error, last_checked_at=now
```

The windows tile: each poll covers exactly `(last_checked_at, now]`, half-open. This means:
- Each ingested log entry is counted exactly once per alert — no gaps, no double counting
- After "Resolved", subsequent polls only cover newly ingested logs — stale logs cannot re-trigger the alert
- Filtering on `ingestion_time()` instead of `timestamp` makes the window immune to App Insights ingestion lag (minutes), which would otherwise silently skip events whose timestamp window had already passed
- No `last_seen_log_id` or watermark is needed; `last_checked_at` is the state

**`AlertService.BuildQueryFromTag`:**
- Accepts `Tag` (with `Filters` JSONB) and `Source` (with `Type = app_insights`)
- Produces KQL — `union isfuzzy=true traces, exceptions | where ... | where ...`
- Uses the same filter-to-query translation already implemented in `LogQueryService`
- The time range (`[windowStart, windowEnd]`) is injected by `AlertPollingService` at execution time, not baked into the stored query — keeping `query` reusable across polls
- Loki sources are not supported in Phase 5; creating an alert against a Loki source returns HTTP 422

### Frontend architecture

**New files:**

```
frontend/src/
  pages/
    AlertsPage.tsx              # Route /alerts; list + slide-out
  components/
    alerts/
      AlertList.tsx             # Table/list of alert rows with status badges
      AlertSlideover.tsx        # Create/edit form + firing history
      AlertFiringHistory.tsx    # Read-only timestamp+count table
  hooks/
    useAlertsQuery.ts           # GET /alerts
    useCreateAlert.ts           # POST /alerts
    useUpdateAlert.ts           # PUT /alerts/{id}
    useDeleteAlert.ts           # DELETE /alerts/{id}
    useResolveAlert.ts          # POST /alerts/{id}/resolve
    useAlertFiringsQuery.ts     # GET /alerts/{id}/firings
    useAlertStream.ts           # EventSource; dispatches toasts + invalidates queries
```

**Modified files:**

- `AppShell.tsx` — add "Alerts" sidebar nav entry (bell icon); add orange dot indicator; bell click navigates to `/alerts`
- `App.tsx` (or router config) — add `/alerts` route

**`useAlertStream` hook:**
- Opens `EventSource('/api/v1/alerts/stream')`
- On `alert-fired` event: show toast (via shadcn `toast()`), debounced 1 per `alertId` per 60 s (using a `useRef` map of `alertId → lastToastAt`)
- After showing toast: call `queryClient.invalidateQueries(['alerts'])` to refresh the list and update the bell dot
- Cleanup: closes `EventSource` on unmount
- Mounted once in `AppShell.tsx` so it runs across all pages

**Bell dot logic:**
- `useAlertsQuery` returns the alert list
- Bell dot shown when `alerts.some(a => a.status === 'Firing')`
- Click handler: `navigate('/alerts')`

**Alert form validation (react-hook-form + zod):**

```ts
const alertSchema = z.object({
  name: z.string().min(1).max(200),
  sourceId: z.string().uuid(),
  query: z.string().min(1),       // or tagId path handled separately
  checkIntervalSeconds: z.number().int().min(60),
  threshold: z.number().int().min(1),
  enabled: z.boolean(),
})
```

**Status badge colours:**
- `ok` → green (`bg-green-100 text-green-800`)
- `firing` → red (`bg-red-100 text-red-800`)
- `error` → amber (`bg-amber-100 text-amber-800`)

### Migration strategy

One migration required, additive and safe (nullable column, no backfill):

```
backend/Dashy.Api/Infrastructure/Persistence/Migrations/
  20260610000000_AddResolvedAtToAlerts.cs
```

Both `Alert.cs` entity and `AlertConfiguration.cs` updated to include `ResolvedAt DateTime?`.

### Open questions

_All open questions resolved. No outstanding decisions._
