import { useAlertFiringsQuery } from "@/hooks/useAlerts"
import { Loader2Icon } from "lucide-react"

export function AlertFiringHistory({ alertId }: { alertId: string }) {
  const { data: firings, isLoading, error } = useAlertFiringsQuery(alertId)

  if (isLoading) {
    return (
      <div className="flex justify-center py-6">
        <Loader2Icon size={16} className="text-muted-foreground animate-spin" />
      </div>
    )
  }

  if (error) {
    return (
      <p className="py-4 text-center text-[12.5px] text-[var(--sev-error)]">
        Failed to load firing history: {error.message}
      </p>
    )
  }

  if (!firings?.length) {
    return (
      <p className="text-muted-foreground py-4 text-center text-[12.5px]">
        No firings yet.
      </p>
    )
  }

  return (
    <table className="w-full text-[12.5px]">
      <thead>
        <tr className="text-muted-foreground border-border border-b text-left text-[11.5px]">
          <th className="py-1.5 pr-2 font-medium">Fired at</th>
          <th className="py-1.5 text-right font-medium">Results</th>
        </tr>
      </thead>
      <tbody>
        {firings.map((f) => (
          <tr key={f.id} className="border-border/50 border-b last:border-0">
            <td className="py-1.5 pr-2 font-mono">{new Date(f.firedAt).toLocaleString()}</td>
            <td className="py-1.5 text-right font-mono">{f.resultCount}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
