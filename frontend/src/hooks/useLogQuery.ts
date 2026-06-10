import { api, ApiError } from "@/lib/api"
import type { LogEntry, LogQueryRequest } from "@/lib/types"
import { useMutation } from "@tanstack/react-query"
import { useState } from "react"

interface LogQueryResponse {
  entries: LogEntry[]
  hasMore: boolean
}

export interface LogQueryState {
  data: LogEntry[] | null
  hasMore: boolean
  isLoading: boolean
  queryError: string | null    // 400 — invalid KQL, shown inline under SearchBar
  serverError: string | null   // 5xx — shown as toast
  lastUpdatedAt: Date | null   // when the last successful query completed
}

export function useLogQuery() {
  // Set on every successful query — manual search, refresh, or live poll alike.
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null)

  const mutation = useMutation({
    mutationFn: async (req: LogQueryRequest) => {
      // The API may return either LogEntry[] (legacy) or { entries, hasMore }
      const raw = await api.post<LogEntry[] | LogQueryResponse>("/logs/query", req)
      if (Array.isArray(raw)) {
        return { entries: raw, hasMore: false }
      }
      return raw
    },
    onSuccess: () => setLastUpdatedAt(new Date()),
  })

  const queryError =
    mutation.error instanceof ApiError && mutation.error.status === 400
      ? mutation.error.message
      : null

  const serverError =
    mutation.error instanceof ApiError && mutation.error.status !== 400
      ? mutation.error.message
      : mutation.error && !(mutation.error instanceof ApiError)
        ? (mutation.error as Error).message
        : null

  return {
    data: mutation.data?.entries ?? null,
    hasMore: mutation.data?.hasMore ?? false,
    isLoading: mutation.isPending,
    queryError,
    serverError,
    lastUpdatedAt,
    run: mutation.mutate,
    reset: mutation.reset,
  }
}
