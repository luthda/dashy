import { api } from "@/lib/api"
import type { Tag, TagFilters } from "@/lib/types"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

const TAGS_KEY = ["tags"] as const

export function useTagsQuery() {
  return useQuery({
    queryKey: TAGS_KEY,
    queryFn: () => api.get<Tag[]>("/tags"),
  })
}

export function useCreateTag() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { name: string; color?: string; filters?: TagFilters }) =>
      api.post<Tag>("/tags", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: TAGS_KEY }),
  })
}

export function useUpdateTag() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; name?: string; color?: string; filters?: TagFilters }) =>
      api.put<Tag>(`/tags/${id}`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: TAGS_KEY }),
  })
}

export function useDeleteTag() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/tags/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: TAGS_KEY }),
  })
}
