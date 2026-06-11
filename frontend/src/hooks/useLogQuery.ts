import { api, ApiError } from "@/lib/api"
import type { LogEntry, LogQueryRequest, Range } from "@/lib/types"
import { keepPreviousData, useQuery } from "@tanstack/react-query"

const LIVE_MS = 60_000
const PAGE_SIZE = 500

interface LogQueryResponse {
  entries: LogEntry[]
  hasMore: boolean
}

export interface LogQueryParams {
  sourceId: string
  /** The submitted search text — not the live input value. */
  query: string
  range: Range
  page: number
  /** Sorted for a stable query key. */
  tagIds: string[]
  /** Server-side event-type filter; undefined = all types. */
  eventTypes?: string[]
  /** Live mode polls every 60s while true. */
  live: boolean
}

/**
 * Declarative log query: any param change refetches via the query key, the
 * cache survives navigation, and live mode is just a refetch interval. No
 * imperative run() — callers change state, the key reacts.
 */
export function useLogQuery(params: LogQueryParams) {
  const { sourceId, query, range, page, tagIds, eventTypes, live } = params

  const result = useQuery({
    queryKey: ["logs", sourceId, range, query, page, tagIds, eventTypes ?? null] as const,
    queryFn: async () => {
      const req: LogQueryRequest = {
        sourceId,
        query: query || undefined,
        tagIds: tagIds.length > 0 ? tagIds : undefined,
        eventTypes,
        timeRange: { type: "relative", value: range },
        limit: PAGE_SIZE,
        skip: page * PAGE_SIZE,
      }
      // The API may return either LogEntry[] (legacy) or { entries, hasMore }
      const raw = await api.post<LogEntry[] | LogQueryResponse>("/logs/query", req)
      return Array.isArray(raw) ? { entries: raw, hasMore: false } : raw
    },
    enabled: !!sourceId,
    // Keep the previous rows visible while the next fetch runs — no skeleton
    // flash when switching source, page, or filters.
    placeholderData: keepPreviousData,
    refetchInterval: live ? LIVE_MS : false,
    // Paused means paused: no surprise fetches on tab focus.
    refetchOnWindowFocus: false,
    // 400 = invalid KQL; retrying cannot fix it.
    retry: (failureCount, error) =>
      !(error instanceof ApiError && error.status === 400) && failureCount < 1,
  })

  const queryError =
    result.error instanceof ApiError && result.error.status === 400
      ? result.error.message
      : null

  const serverError =
    result.error instanceof ApiError && result.error.status !== 400
      ? result.error.message
      : result.error && !(result.error instanceof ApiError)
        ? result.error.message
        : null

  return {
    data: result.data?.entries ?? null,
    hasMore: result.data?.hasMore ?? false,
    /** First load with nothing cached for this key — show the skeleton. */
    isLoading: result.isPending,
    /** Any in-flight fetch — drives the refresh spinner. */
    isFetching: result.isFetching,
    queryError,
    serverError,
    /** When the data on screen was last fetched; null until the first success. */
    lastUpdatedAt: result.dataUpdatedAt ? new Date(result.dataUpdatedAt) : null,
    refetch: result.refetch,
  }
}
