import { AlertDialog } from "@/components/alerts/AlertDialog"
import { AlertList } from "@/components/alerts/AlertList"
import { useAlertsQuery } from "@/hooks/useAlerts"
import type { Alert } from "@/lib/types"
import { Loader2Icon, PlusIcon } from "lucide-react"
import { useState } from "react"

export function AlertsPage() {
  const { data: alerts, isLoading, error } = useAlertsQuery()
  const [showCreate, setShowCreate] = useState(false)
  const [editing, setEditing] = useState<Alert | null>(null)

  if (isLoading) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <Loader2Icon size={20} className="text-muted-foreground animate-spin" />
      </div>
    )
  }

  return (
    <div className="flex max-w-3xl flex-col gap-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-[15px] font-semibold">Alerts</h1>
          <p className="text-muted-foreground mt-0.5 text-[12.5px]">
            Get notified when log queries cross a threshold.
          </p>
        </div>
        <button
          onClick={() => setShowCreate(true)}
          className="bg-primary text-primary-foreground flex h-9 items-center gap-2 rounded-md px-3 text-[13px] font-medium transition-opacity hover:opacity-90"
        >
          <PlusIcon size={14} />
          Create
        </button>
      </div>

      {error ? (
        <p className="py-8 text-center text-[12.5px] text-[var(--sev-error)]">
          Failed to load alerts: {error.message}
        </p>
      ) : alerts?.length === 0 ? (
        <EmptyState onCreate={() => setShowCreate(true)} />
      ) : (
        <AlertList alerts={alerts ?? []} onEdit={setEditing} />
      )}

      {showCreate && <AlertDialog alert={null} onClose={() => setShowCreate(false)} />}
      {editing && <AlertDialog alert={editing} onClose={() => setEditing(null)} />}
    </div>
  )
}

function EmptyState({ onCreate }: { onCreate: () => void }) {
  return (
    <div className="flex flex-col items-center gap-3 py-16 text-center">
      <div className="text-[14px] font-medium">No alerts yet</div>
      <p className="text-muted-foreground max-w-xs text-[12.5px]">
        Create one to start monitoring.
      </p>
      <button
        onClick={onCreate}
        className="bg-primary text-primary-foreground mt-2 h-9 rounded-md px-4 text-[13px] font-medium transition-opacity hover:opacity-90"
      >
        Create an alert
      </button>
    </div>
  )
}
