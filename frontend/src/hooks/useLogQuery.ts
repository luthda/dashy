import { api, ApiError } from "@/lib/api"
import type { LogEntry, LogQueryRequest } from "@/lib/types"
import { useMutation } from "@tanstack/react-query"

interface LogQueryResponse {
  entries: LogEntry[]
  totalCount: number
}

export interface LogQueryState {
  data: LogEntry[] | null
  totalCount: number
  isLoading: boolean
  queryError: string | null    // 400 — invalid KQL, shown inline under SearchBar
  serverError: string | null   // 5xx — shown as toast
}

export function useLogQuery() {
  const mutation = useMutation({
    mutationFn: async (req: LogQueryRequest) => {
      // The API may return either LogEntry[] (legacy) or { items, totalCount }
      const raw = await api.post<LogEntry[] | LogQueryResponse>("/logs/query", req)
      if (Array.isArray(raw)) {
        return { entries: raw, totalCount: raw.length }
      }
      return raw
    },
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
    totalCount: mutation.data?.totalCount ?? 0,
    isLoading: mutation.isPending,
    queryError,
    serverError,
    run: mutation.mutate,
    reset: mutation.reset,
  }
}
