import { useResolveAlert } from "@/hooks/useAlerts"
import { cn } from "@/lib/utils"
import type { Alert, AlertStatus } from "@/lib/types"
import { Loader2Icon } from "lucide-react"

const STATUS_STYLES: Record<AlertStatus, { label: string; cls: string }> = {
  Ok: { label: "OK", cls: "text-[var(--sev-success)] bg-[var(--sev-success)]/10" },
  Firing: { label: "Firing", cls: "text-[var(--sev-error)] bg-[var(--sev-error)]/10" },
  Error: { label: "Error", cls: "text-[var(--sev-warn)] bg-[var(--sev-warn)]/10" },
}

interface AlertListProps {
  alerts: Alert[]
  onEdit: (alert: Alert) => void
}

export function AlertList({ alerts, onEdit }: AlertListProps) {
  return (
    <div className="flex flex-col gap-2">
      {alerts.map((alert) => (
        <AlertRow key={alert.id} alert={alert} onEdit={() => onEdit(alert)} />
      ))}
    </div>
  )
}

function AlertRow({ alert, onEdit }: { alert: Alert; onEdit: () => void }) {
  const resolve = useResolveAlert()
  const status = STATUS_STYLES[alert.status]

  return (
    <div
      onClick={onEdit}
      className="border-border bg-card hover:border-ring/40 flex cursor-pointer items-center gap-3 rounded-lg border px-4 py-3 transition-colors"
    >
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="truncate text-[13.5px] font-medium">{alert.name}</span>
          {!alert.enabled && (
            <span className="text-muted-foreground bg-muted rounded px-1.5 py-0.5 text-[10.5px] font-medium">
              Disabled
            </span>
          )}
        </div>
        <div className="text-muted-foreground mt-0.5 text-[11.5px]">{alert.sourceName}</div>
      </div>

      <span
        className={cn(
          "rounded-full px-2 py-0.5 text-[11px] font-semibold whitespace-nowrap",
          status.cls,
        )}
      >
        {status.label}
      </span>

      <span
        title="Last checked"
        className="text-muted-foreground hidden w-36 text-right font-mono text-[11.5px] whitespace-nowrap sm:block"
      >
        {alert.lastCheckedAt ? new Date(alert.lastCheckedAt).toLocaleString() : "never checked"}
      </span>

      {alert.status === "Firing" && (
        <button
          onClick={(e) => {
            e.stopPropagation()
            resolve.mutate(alert.id)
          }}
          disabled={resolve.isPending}
          className="border-border hover:bg-accent flex h-7 items-center gap-1.5 rounded-md border px-2.5 text-[12px] font-medium transition-colors"
        >
          {resolve.isPending && <Loader2Icon size={12} className="animate-spin" />}
          Resolved
        </button>
      )}
    </div>
  )
}
