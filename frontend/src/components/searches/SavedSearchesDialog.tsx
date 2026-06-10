import { Field, inputCls } from "@/components/shared/Field"
import {
  useCreateSavedSearch,
  useDeleteSavedSearch,
  useSavedSearchesQuery,
  useUpdateSavedSearch,
} from "@/hooks/useSavedSearches"
import type { SavedSearch } from "@/lib/types"
import { CheckIcon, Loader2Icon, PencilIcon, SearchIcon, Trash2Icon, XIcon } from "lucide-react"
import { useEffect, useRef, useState } from "react"

interface SavedSearchesDialogProps {
  /** The query string currently in the search bar — offered as the value to save. */
  currentQuery: string
  /** Load a saved search's string into the search bar and run it. */
  onApply: (query: string) => void
  onClose: () => void
}

export function SavedSearchesDialog({ currentQuery, onApply, onClose }: SavedSearchesDialogProps) {
  const { data: searches } = useSavedSearchesQuery()

  useEffect(() => {
    function handleKey(e: KeyboardEvent) {
      if (e.key === "Escape") onClose()
    }
    document.addEventListener("keydown", handleKey)
    return () => document.removeEventListener("keydown", handleKey)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm" onClick={onClose}>
      <div className="bg-card border-border mx-4 w-full max-w-md rounded-xl border p-6 shadow-xl" onClick={(e) => e.stopPropagation()}>
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-[15px] font-semibold">Saved searches</h2>
          <button onClick={onClose} className="text-muted-foreground hover:text-foreground">
            <XIcon size={16} />
          </button>
        </div>

        <SaveCurrent currentQuery={currentQuery} />

        <div className="bg-border my-4 h-px" />

        <SavedList
          searches={searches ?? []}
          onApply={(q) => {
            onApply(q)
            onClose()
          }}
        />
      </div>
    </div>
  )
}

function SaveCurrent({ currentQuery }: { currentQuery: string }) {
  const create = useCreateSavedSearch()
  const [name, setName] = useState("")

  const trimmedName = name.trim()
  const canSave = trimmedName.length > 0 && !create.isPending

  async function handleSave() {
    if (!canSave) return
    await create.mutateAsync({ name: trimmedName, query: currentQuery })
    setName("")
  }

  return (
    <div className="flex flex-col gap-2.5">
      <Field label="Save current search" hint={currentQuery ? undefined : "empty query"}>
        <div className="border-border bg-background flex items-center gap-2 rounded-md border px-2.5 py-1.5">
          <SearchIcon size={13} className="text-muted-foreground shrink-0" />
          <span className="text-foreground/80 truncate text-[12.5px]">
            {currentQuery || <span className="text-muted-foreground/60">All logs (no filter)</span>}
          </span>
        </div>
      </Field>

      <div className="flex items-end gap-2">
        <div className="flex-1">
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault()
                handleSave()
              }
            }}
            placeholder="Name this search…"
            className={inputCls + " w-full"}
          />
        </div>
        <button
          type="button"
          onClick={handleSave}
          disabled={!canSave}
          className="bg-primary text-primary-foreground flex h-9 items-center gap-2 rounded-md px-4 text-[13px] font-medium transition-opacity hover:opacity-90 disabled:opacity-40"
        >
          {create.isPending && <Loader2Icon size={13} className="animate-spin" />}
          Save
        </button>
      </div>

      {create.error && (
        <p className="text-[12px] text-[var(--sev-error)]">{create.error.message}</p>
      )}
    </div>
  )
}

function SavedList({
  searches,
  onApply,
}: {
  searches: SavedSearch[]
  onApply: (query: string) => void
}) {
  const deleteSearch = useDeleteSavedSearch()
  const updateSearch = useUpdateSavedSearch()
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editName, setEditName] = useState("")
  const editRef = useRef<HTMLInputElement>(null)

  function startEdit(s: SavedSearch) {
    setEditingId(s.id)
    setEditName(s.name)
    setConfirmDeleteId(null)
    setTimeout(() => editRef.current?.focus(), 0)
  }

  async function submitEdit(id: string) {
    const trimmed = editName.trim()
    if (!trimmed) return
    await updateSearch.mutateAsync({ id, name: trimmed })
    setEditingId(null)
  }

  if (searches.length === 0) {
    return (
      <p className="text-muted-foreground py-4 text-center text-[13px]">
        No saved searches yet. Name a search above to recall it later.
      </p>
    )
  }

  return (
    <div className="flex flex-col gap-1.5">
      <span className="text-muted-foreground mb-0.5 text-[12.5px] font-medium">Saved</span>
      {searches.map((s) => (
        <div
          key={s.id}
          className="border-border hover:bg-accent group flex items-center gap-2.5 rounded-lg border px-3 py-2"
        >
          {editingId === s.id ? (
            <div className="flex min-w-0 flex-1 items-center gap-1.5">
              <input
                ref={editRef}
                value={editName}
                onChange={(e) => setEditName(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") { e.preventDefault(); submitEdit(s.id) }
                  if (e.key === "Escape") setEditingId(null)
                  e.stopPropagation()
                }}
                className="border-border bg-background min-w-0 flex-1 rounded-md border px-2 py-0.5 text-[13px] outline-none"
              />
              <button
                type="button"
                onClick={() => submitEdit(s.id)}
                disabled={!editName.trim() || updateSearch.isPending}
                className="text-muted-foreground hover:text-foreground shrink-0"
              >
                <CheckIcon size={13} />
              </button>
              <button
                type="button"
                onClick={() => setEditingId(null)}
                className="text-muted-foreground hover:text-foreground shrink-0"
              >
                <XIcon size={13} />
              </button>
            </div>
          ) : (
            <button
              type="button"
              onClick={() => onApply(s.query)}
              className="flex min-w-0 flex-1 flex-col items-start text-left"
            >
              <span className="text-[13px] font-medium">{s.name}</span>
              <span className="text-muted-foreground truncate text-[11.5px]">
                {s.query || "All logs (no filter)"}
              </span>
            </button>
          )}
          {editingId !== s.id && (
            <>
              <button
                type="button"
                onClick={() => startEdit(s)}
                className="text-muted-foreground shrink-0 hover:text-foreground"
                title="Rename"
              >
                <PencilIcon size={13} />
              </button>
              {confirmDeleteId === s.id ? (
                <button
                  type="button"
                  onClick={() => { deleteSearch.mutate(s.id); setConfirmDeleteId(null) }}
                  disabled={deleteSearch.isPending}
                  className="shrink-0 text-[11.5px] font-medium text-[var(--sev-error)]"
                >
                  Confirm
                </button>
              ) : (
                <button
                  type="button"
                  onClick={() => { setConfirmDeleteId(s.id); setEditingId(null) }}
                  className="text-muted-foreground shrink-0 hover:text-[var(--sev-error)]"
                  title="Delete saved search"
                >
                  <Trash2Icon size={13} />
                </button>
              )}
            </>
          )}
        </div>
      ))}
      {updateSearch.error && (
        <p className="text-[12px] text-[var(--sev-error)]">{updateSearch.error.message}</p>
      )}
    </div>
  )
}
