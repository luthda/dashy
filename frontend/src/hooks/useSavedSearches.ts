import { api } from "@/lib/api"
import type { SavedSearch } from "@/lib/types"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

const SAVED_SEARCHES_KEY = ["saved-searches"] as const

export function useSavedSearchesQuery() {
  return useQuery({
    queryKey: SAVED_SEARCHES_KEY,
    queryFn: () => api.get<SavedSearch[]>("/saved-searches"),
  })
}

export function useCreateSavedSearch() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { name: string; query: string }) =>
      api.post<SavedSearch>("/saved-searches", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: SAVED_SEARCHES_KEY }),
  })
}

export function useUpdateSavedSearch() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) =>
      api.put<SavedSearch>(`/saved-searches/${id}`, { name }),
    onSuccess: () => qc.invalidateQueries({ queryKey: SAVED_SEARCHES_KEY }),
  })
}

export function useDeleteSavedSearch() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/saved-searches/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: SAVED_SEARCHES_KEY }),
  })
}
