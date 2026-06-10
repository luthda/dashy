import { api } from "@/lib/api"
import type { Alert, AlertFiring, AlertUpsertRequest } from "@/lib/types"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

const ALERTS_KEY = ["alerts"] as const

export function useAlertsQuery() {
  return useQuery({
    queryKey: ALERTS_KEY,
    queryFn: () => api.get<Alert[]>("/alerts"),
    // Keeps the sidebar bell dot current even without the SSE stream.
    refetchInterval: 60_000,
  })
}

export function useCreateAlert() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: AlertUpsertRequest) => api.post<Alert>("/alerts", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ALERTS_KEY }),
  })
}

export function useUpdateAlert() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...body }: AlertUpsertRequest & { id: string }) =>
      api.put<Alert>(`/alerts/${id}`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ALERTS_KEY }),
  })
}

export function useDeleteAlert() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/alerts/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ALERTS_KEY }),
  })
}

export function useResolveAlert() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<Alert>(`/alerts/${id}/resolve`, {}),
    onSuccess: () => qc.invalidateQueries({ queryKey: ALERTS_KEY }),
  })
}

export function useAlertFiringsQuery(alertId: string | undefined) {
  return useQuery({
    queryKey: [...ALERTS_KEY, alertId, "firings"],
    queryFn: () => api.get<AlertFiring[]>(`/alerts/${alertId}/firings`),
    enabled: !!alertId,
  })
}
