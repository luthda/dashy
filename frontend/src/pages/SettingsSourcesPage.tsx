import { SourceSetupDialog } from "@/components/sources/SourceSetupDialog"
import { useDeleteSource, useSourcesQuery, useTestConnection } from "@/hooks/useSources"
import { cn } from "@/lib/utils"
import type { Source } from "@/lib/types"
import {
  CheckCircle2Icon,
  Loader2Icon,
  PlusIcon,
  Trash2Icon,
  XCircleIcon,
} from "lucide-react"
import { useState } from "react"

export function SettingsSourcesPage() {
  const { data: sources, isLoading } = useSourcesQuery()
  const [showAdd, setShowAdd] = useState(false)
  const [editing, setEditing] = useState<Source | null>(null)

  if (isLoading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <Loader2Icon size={20} className="animate-spin text-muted-foreground" />
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-6 p-6 max-w-2xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-[15px] font-semibold">Sources</h1>
          <p className="text-[12.5px] text-muted-foreground mt-0.5">
            Connect log sources to query in Dashy.
          </p>
        </div>
        <button
          onClick={() => setShowAdd(true)}
          className="h-9 px-3 rounded-md bg-primary text-primary-foreground text-[13px] font-medium flex items-center gap-2 hover:opacity-90 transition-opacity"
        >
          <PlusIcon size={14} />
          Add source
        </button>
      </div>

      {sources?.length === 0 ? (
        <EmptyState onAdd={() => setShowAdd(true)} />
      ) : (
        <div className="flex flex-col gap-2">
          {sources?.map((s) => (
            <SourceRow
              key={s.id}
              source={s}
              onEdit={() => setEditing(s)}
            />
          ))}
        </div>
      )}

      {showAdd && <SourceSetupDialog onClose={() => setShowAdd(false)} />}
      {editing && (
        <SourceSetupDialog source={editing} onClose={() => setEditing(null)} />
      )}
    </div>
  )
}

function SourceRow({ source, onEdit }: { source: Source; onEdit: () => void }) {
  const del  = useDeleteSource()
  const test = useTestConnection()

  return (
    <div className="flex items-center gap-3 p-4 rounded-lg border border-border bg-card">
      <div
        className="w-8 h-8 rounded-md grid place-items-center text-[10px] font-bold text-white flex-shrink-0 bg-[var(--sev-info)]"
      >
        Az
      </div>

      <div className="flex-1 min-w-0">
        <div className="text-[13.5px] font-medium truncate">{source.name}</div>
        <div className="text-[11.5px] text-muted-foreground">Azure App Insights</div>
      </div>

      {/* Test result */}
      {test.isPending && <Loader2Icon size={14} className="animate-spin text-muted-foreground" />}
      {test.data?.ok === true  && <CheckCircle2Icon size={14} className="text-[var(--sev-info)]" />}
      {test.data?.ok === false && <XCircleIcon size={14} className="text-[var(--sev-error)]" />}

      <button
        onClick={() => test.mutate(source.id)}
        className="text-[12px] text-muted-foreground hover:text-foreground transition-colors"
      >
        Test
      </button>
      <button
        onClick={onEdit}
        className="text-[12px] text-muted-foreground hover:text-foreground transition-colors"
      >
        Edit
      </button>
      <button
        onClick={() => del.mutate(source.id)}
        disabled={del.isPending}
        className={cn(
          "h-7 w-7 rounded-md grid place-items-center transition-colors",
          "text-muted-foreground hover:text-[var(--sev-error)] hover:bg-[var(--sev-error)]/10",
        )}
        aria-label="Delete source"
      >
        {del.isPending ? (
          <Loader2Icon size={13} className="animate-spin" />
        ) : (
          <Trash2Icon size={13} />
        )}
      </button>
    </div>
  )
}

function EmptyState({ onAdd }: { onAdd: () => void }) {
  return (
    <div className="flex flex-col items-center gap-3 py-16 text-center">
      <div className="text-[14px] font-medium">No sources yet</div>
      <p className="text-[12.5px] text-muted-foreground max-w-xs">
        Connect Azure App Insights to start querying logs.
      </p>
      <button
        onClick={onAdd}
        className="mt-2 h-9 px-4 rounded-md bg-primary text-primary-foreground text-[13px] font-medium hover:opacity-90 transition-opacity"
      >
        Connect a source
      </button>
    </div>
  )
}
