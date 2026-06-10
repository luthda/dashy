# Data Fetching Patterns

Reference for the API client, TanStack React Query hooks, and SSE.

---

## API Client

All HTTP calls go through `lib/api.ts` — typed fetch wrappers over the .NET API.
Components and hooks never call `fetch` directly.

```typescript
// lib/api.ts
const BASE = "/api/v1"

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { "Content-Type": "application/json", ...init?.headers },
    ...init,
  })

  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new ApiError(res.status, body?.error ?? res.statusText, body)
  }

  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

export class ApiError extends Error {
  readonly status: number
  readonly body?: unknown

  constructor(status: number, message: string, body?: unknown) {
    super(message)
    this.name   = "ApiError"
    this.status = status
    this.body   = body
  }
}

export const api = {
  get:    <T>(path: string)                => request<T>(path),
  post:   <T>(path: string, body: unknown) => request<T>(path, { method: "POST",   body: JSON.stringify(body) }),
  put:    <T>(path: string, body: unknown) => request<T>(path, { method: "PUT",    body: JSON.stringify(body) }),
  delete: <T>(path: string)               => request<T>(path, { method: "DELETE" }),
}
```

---

## Hook File Structure

One hook file per domain in `hooks/`. Each file exports **both** query and mutation hooks for
that domain. Query keys are file-local constants — no centralized key factory.

```typescript
// hooks/useSources.ts
import { api } from "@/lib/api"
import type { Source, SourceType } from "@/lib/types"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

const SOURCES_KEY = ["sources"] as const

export function useSourcesQuery() {
  return useQuery({
    queryKey: SOURCES_KEY,
    queryFn: () => api.get<Source[]>("/sources"),
  })
}

export function useCreateSource() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { name: string; type: SourceType; config: object }) =>
      api.post<Source>("/sources", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: SOURCES_KEY }),
  })
}

export function useUpdateSource() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; name?: string; config?: object }) =>
      api.put<Source>(`/sources/${id}`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: SOURCES_KEY }),
  })
}

export function useDeleteSource() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/sources/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: SOURCES_KEY }),
  })
}
```

```typescript
// hooks/useTags.ts — same pattern
const TAGS_KEY = ["tags"] as const

export function useTagsQuery() { ... }
export function useCreateTag() { ... }
export function useUpdateTag() { ... }
export function useDeleteTag() { ... }
```

Rules:
- Query key is a file-local `as const` array at the top of the file.
- Query and mutation hooks for the same domain live in the same file.
- Always invalidate the relevant key(s) in `onSuccess`.
- Return the full `useMutation` / `useQuery` result — let the component destructure.
- Toast on success/failure in the component, not the hook — keeps hooks reusable.

---

## Log Query Hook

The log query is a **mutation**, not a query — it fires on user submit, not on mount. It
returns a custom state object that separates query errors (400 — invalid KQL, shown inline)
from server errors (5xx — shown as toast).

```typescript
// hooks/useLogQuery.ts
import { api, ApiError } from "@/lib/api"
import type { LogEntry, LogQueryRequest } from "@/lib/types"
import { useMutation } from "@tanstack/react-query"
import { useState } from "react"

export interface LogQueryState {
  data: LogEntry[] | null
  hasMore: boolean
  isLoading: boolean
  queryError: string | null    // 400 — shown inline under SearchBar
  serverError: string | null   // 5xx — shown as toast
  lastUpdatedAt: Date | null
}

export function useLogQuery() {
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null)

  const mutation = useMutation({
    mutationFn: async (req: LogQueryRequest) => {
      const raw = await api.post<LogEntry[] | { entries: LogEntry[]; hasMore: boolean }>("/logs/query", req)
      return Array.isArray(raw) ? { entries: raw, hasMore: false } : raw
    },
    onSuccess: () => setLastUpdatedAt(new Date()),
  })

  const queryError =
    mutation.error instanceof ApiError && mutation.error.status === 400
      ? mutation.error.message : null

  const serverError =
    mutation.error instanceof ApiError && mutation.error.status !== 400
      ? mutation.error.message
      : mutation.error && !(mutation.error instanceof ApiError)
        ? (mutation.error as Error).message : null

  return {
    data:          mutation.data?.entries ?? null,
    hasMore:       mutation.data?.hasMore ?? false,
    isLoading:     mutation.isPending,
    queryError,
    serverError,
    lastUpdatedAt,
    run:   mutation.mutate,
    reset: mutation.reset,
  }
}
```

Usage in components: call `run(params)` to fire the query, read `data` / `isLoading` /
`queryError` / `serverError` from the returned state.

---

## Connection Test

Testing a source connection is an action. Use `useMutation` (already in `useSources.ts`).

```typescript
// hooks/useSources.ts
export function useTestConnection() {
  return useMutation({
    mutationFn: (id: string) => api.post<ConnectionTestResult>(`/sources/${id}/test`, {}),
  })
}
```

---

## SSE — Alert Stream

`hooks/useAlertStream.ts` opens an `EventSource` to `/api/v1/alerts/stream` and dispatches
toast notifications when alerts fire. This hook is called once in `AppShell` so the connection
lives for the app's lifetime.

> **Status**: implementing in Phase 5.

```typescript
// hooks/useAlertStream.ts
import { useEffect, useRef } from "react"
import { useToast } from "@/hooks/use-toast"
import { useQueryClient } from "@tanstack/react-query"

const ALERTS_KEY = ["alerts"] as const

type AlertFiredEvent = { alertId: string; alertName: string; resultCount: number }

export function useAlertStream() {
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const lastFiredRef = useRef<Map<string, number>>(new Map())

  useEffect(() => {
    const source = new EventSource("/api/v1/alerts/stream")

    source.addEventListener("alert-fired", (event) => {
      const data: AlertFiredEvent = JSON.parse(event.data)

      const now = Date.now()
      const lastFired = lastFiredRef.current.get(data.alertId) ?? 0
      if (now - lastFired < 60_000) return  // debounce per alert
      lastFiredRef.current.set(data.alertId, now)

      toast({
        title: `Alert: ${data.alertName}`,
        description: `${data.resultCount} matching logs found.`,
        duration: 8000,
      })

      queryClient.invalidateQueries({ queryKey: ALERTS_KEY })
    })

    return () => source.close()
  }, [toast, queryClient])
}
```

---

## Error Handling in Components

```typescript
const { data: sources, isLoading, error } = useSourcesQuery()

if (isLoading) return <LoadingSkeleton />
if (error) return <ErrorBanner message={error.message} />
```

The `ApiError` class carries `.status` and `.body`. Components can branch on status:

```typescript
if (error instanceof ApiError && error.status === 502) {
  return <ErrorBanner message="Log source is unreachable." />
}
```

For `useLogQuery`, branch on `queryError` (show inline) vs. `serverError` (show as toast).

---

## Types

Shared API and domain types live in `lib/types.ts` (not `types/api.ts`).

```typescript
// lib/types.ts
export const SourceType = {
  AppInsights: "AppInsights",
} as const
export type SourceType = (typeof SourceType)[keyof typeof SourceType]

export interface Source { id: string; name: string; type: SourceType; createdAt: string }

export interface LogEntry {
  timestamp: string
  level: "error" | "warn" | "info" | "debug" | "trace"
  message: string
  source: string
  eventType: string
  properties: Record<string, string>
}

export interface LogQueryRequest {
  sourceId: string
  query?: string
  tagIds?: string[]
  eventTypes?: string[]
  timeRange?: TimeRange
  limit?: number
  skip?: number
}

export interface Tag {
  id: string; name: string; color: string
  filters: { terms: string[]; levels: string[]; eventTypes: string[] }
  createdAt: string
}
// ... SavedSearch, TimeRange, ConnectionTestResult, etc.
```

Use `interface` (not `type`) for object shapes. Import from `@/lib/types`.
