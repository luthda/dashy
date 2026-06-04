import { api, ApiError } from "@/lib/api"
import type { LogEntry, LogQueryRequest } from "@/lib/types"
import { useMutation } from "@tanstack/react-query"

export interface LogQueryState {
  data: LogEntry[] | null
  isLoading: boolean
  queryError: string | null    // 400 — invalid KQL, shown inline under SearchBar
  serverError: string | null   // 5xx — shown as toast
}

export function useLogQuery() {
  const mutation = useMutation({
    mutationFn: (req: LogQueryRequest) => api.post<LogEntry[]>("/logs/query", req),
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
    data: mutation.data ?? null,
    isLoading: mutation.isPending,
    queryError,
    serverError,
    run: mutation.mutate,
    reset: mutation.reset,
  }
}
