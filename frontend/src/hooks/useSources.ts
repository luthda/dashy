import { api } from "@/lib/api"
import type { ConnectionTestResult, Source, SourceType } from "@/lib/types"
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

export function useTestConnection() {
  return useMutation({
    mutationFn: (id: string) => api.post<ConnectionTestResult>(`/sources/${id}/test`, {}),
  })
}
