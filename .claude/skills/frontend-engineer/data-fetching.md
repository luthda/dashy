# Data Fetching Patterns

Reference for the API client, TanStack React Query hooks, mutations, and SSE.

---

## API Client

All HTTP calls go through `lib/api.ts` — typed fetch wrappers over the .NET API.
Components and hooks never call `fetch` directly.

```typescript
// lib/api.ts
const BASE_URL = "/api/v1"

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    headers: { "Content-Type": "application/json" },
    ...options,
  })

  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    throw new ApiError(response.status, problem?.detail ?? response.statusText)
  }

  if (response.status === 204) return undefined as T
  return response.json()
}

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message)
  }
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body: unknown) =>
    request<T>(path, { method: "POST", body: JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) =>
    request<T>(path, { method: "PUT", body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: "DELETE" }),
}
```

---

## Query Key Factory

Centralize all query keys in `hooks/queries/keys.ts` for consistent invalidation.

```typescript
// hooks/queries/keys.ts
export const queryKeys = {
  sources: {
    all: ["sources"] as const,
    detail: (id: string) => ["sources", id] as const,
  },
  tags: {
    all: ["tags"] as const,
  },
  savedSearches: {
    all: ["saved-searches"] as const,
  },
  alerts: {
    all: ["alerts"] as const,
    detail: (id: string) => ["alerts", id] as const,
    firings: (alertId: string) => ["alerts", alertId, "firings"] as const,
  },
  logs: {
    query: (params: LogQueryParams) => ["logs", "query", params] as const,
  },
}
```

---

## Query Hooks

One file per domain in `hooks/queries/`. Each hook wraps `useQuery` with proper typing.

```typescript
// hooks/queries/useSourcesQuery.ts
import { useQuery } from "@tanstack/react-query"
import { api } from "@/lib/api"
import { queryKeys } from "./keys"
import type { SourceResponse } from "@/types/api"

export function useSourcesQuery() {
  return useQuery({
    queryKey: queryKeys.sources.all,
    queryFn: () => api.get<SourceResponse[]>("/sources"),
  })
}
```

```typescript
// hooks/queries/useAlertsQuery.ts
export function useAlertsQuery() {
  return useQuery({
    queryKey: queryKeys.alerts.all,
    queryFn: () => api.get<AlertResponse[]>("/alerts"),
  })
}

export function useAlertFiringsQuery(alertId: string) {
  return useQuery({
    queryKey: queryKeys.alerts.firings(alertId),
    queryFn: () => api.get<AlertFiringResponse[]>(`/alerts/${alertId}/firings`),
    enabled: !!alertId,
  })
}
```

Rules:
- Always specify `queryKey` from the key factory.
- Use `enabled` to conditionally skip queries (e.g. when an ID isn't selected yet).
- Return the full `useQuery` result — let the component destructure `{ data, isLoading, error }`.

---

## Log Query Hook

The log query is a POST with a request body, but semantically it's a read. Use `useQuery` with
`queryFn` that calls `api.post`.

```typescript
// hooks/queries/useLogQuery.ts
import { useQuery } from "@tanstack/react-query"
import { api } from "@/lib/api"
import { queryKeys } from "./keys"
import type { LogQueryParams, LogEntry } from "@/types/api"

export function useLogQuery(params: LogQueryParams | null) {
  return useQuery({
    queryKey: queryKeys.logs.query(params!),
    queryFn: () => api.post<LogEntry[]>("/logs/query", params),
    enabled: params !== null,
  })
}
```

The `params` object includes `sourceId`, `query`, `tagIds`, `timeRange`, and `limit`. The hook
is disabled until the user submits a search.

---

## Mutation Hooks

One file per mutation in `hooks/mutations/`. Each hook wraps `useMutation` with proper typing
and query invalidation.

```typescript
// hooks/mutations/useCreateSource.ts
import { useMutation, useQueryClient } from "@tanstack/react-query"
import { api } from "@/lib/api"
import { queryKeys } from "@/hooks/queries/keys"
import type { CreateSourceRequest, SourceResponse } from "@/types/api"

export function useCreateSource() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: CreateSourceRequest) =>
      api.post<SourceResponse>("/sources", data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.sources.all })
    },
  })
}
```

```typescript
// hooks/mutations/useDeleteAlert.ts
export function useDeleteAlert() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => api.delete(`/alerts/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.alerts.all })
    },
  })
}
```

Rules:
- Invalidate the relevant query key(s) in `onSuccess`.
- Toast on success/failure in the component, not the hook — keeps hooks reusable.
- Mutation hooks don't manage dialog state — the component does.

---

## Connection Test

Testing a source connection is an action, not a query. Use `useMutation`.

```typescript
// hooks/mutations/useTestConnection.ts
export function useTestConnection() {
  return useMutation({
    mutationFn: (sourceId: string) =>
      api.post<TestConnectionResponse>(`/sources/${sourceId}/test`, {}),
  })
}
```

---

## SSE — Alert Stream

`hooks/useAlertStream.ts` opens an `EventSource` connection to `/api/v1/alerts/stream` and
dispatches toast notifications when alerts fire.

```typescript
// hooks/useAlertStream.ts
import { useEffect, useRef } from "react"
import { useToast } from "@/hooks/use-toast"
import { useQueryClient } from "@tanstack/react-query"
import { queryKeys } from "./queries/keys"

type AlertFiredEvent = {
  alertId: string
  alertName: string
  resultCount: number
}

export function useAlertStream() {
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const lastFiredRef = useRef<Map<string, number>>(new Map())

  useEffect(() => {
    const source = new EventSource("/api/v1/alerts/stream")

    source.onmessage = (event) => {
      const data: AlertFiredEvent = JSON.parse(event.data)

      const now = Date.now()
      const lastFired = lastFiredRef.current.get(data.alertId) ?? 0
      if (now - lastFired < 60_000) return
      lastFiredRef.current.set(data.alertId, now)

      toast({
        title: `Alert: ${data.alertName}`,
        description: `${data.resultCount} matching logs found.`,
        duration: 8000,
      })

      queryClient.invalidateQueries({ queryKey: queryKeys.alerts.all })
    }

    return () => source.close()
  }, [toast, queryClient])
}
```

This hook is called once in `AppShell` so the SSE connection lives for the app's lifetime.
The 60-second debounce per alert prevents toast spam from rapid firings.

---

## Auto-Refresh

Saved searches can have a `refreshIntervalSeconds`. Use React Query's `refetchInterval` option.

```typescript
const { data } = useLogQuery(params)
// When a saved search is active with auto-refresh:
// Pass refetchInterval to the query options via a wrapper or direct option
```

The `refetchInterval` is set dynamically based on the active saved search. When no saved search
is active or refresh is manual, `refetchInterval` is `false`.

---

## Error Handling in Components

```typescript
const { data: sources, isLoading, error } = useSourcesQuery()

if (isLoading) return <LoadingSkeleton />
if (error) return <ErrorBanner message={error.message} />
```

The `ApiError` class carries the HTTP status code. Components can branch on status:

```typescript
if (error instanceof ApiError && error.status === 502) {
  return <ErrorBanner message="Log source is unreachable. Check your connection settings." />
}
```

---

## Types

Shared API types live in `types/api.ts`.

```typescript
// types/api.ts
export type SourceResponse = {
  id: string
  name: string
  type: "app_insights" | "loki"
  createdAt: string
}

export type LogEntry = {
  timestamp: string
  level: string
  message: string
  source: string
  eventType: string | null
  properties: Record<string, string>
}

export type LogQueryParams = {
  sourceId: string
  query: string
  tagIds: string[]
  timeRange: TimeRange
  limit: number
}

export type TimeRange =
  | { type: "relative"; value: string }
  | { type: "absolute"; from: string; to: string }

export type AlertResponse = {
  id: string
  name: string
  sourceId: string
  query: string
  checkIntervalSeconds: number
  threshold: number
  enabled: boolean
  lastCheckedAt: string | null
  status: "ok" | "firing" | "error"
  createdAt: string
}

export type AlertFiringResponse = {
  id: string
  alertId: string
  firedAt: string
  resultCount: number
}

export type TagResponse = {
  id: string
  name: string
  color: string
  filters: { terms: string[]; levels: string[]; eventTypes: string[] }
  createdAt: string
}

export type SavedSearchResponse = {
  id: string
  name: string
  sourceId: string
  query: string
  tagIds: string[]
  timeRange: TimeRange
  refreshIntervalSeconds: number | null
  createdAt: string
}

export type TestConnectionResponse = {
  ok: boolean
  error?: string
}
```
