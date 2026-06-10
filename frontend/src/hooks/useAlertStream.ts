import { useQueryClient } from "@tanstack/react-query"
import { useEffect, useRef } from "react"
import { toast } from "sonner"

interface AlertFiredEvent {
  alertId: string
  alertName: string
  resultCount: number
  firedAt: string
}

const TOAST_DURATION_MS = 10_000
// At most one toast per alert per minute, even if it fires on every poll tick.
const TOAST_DEBOUNCE_MS = 60_000

/**
 * Listens to the alert SSE stream and surfaces firings as toasts.
 * Mount once (in AppShell) so notifications work on every page.
 * The browser's EventSource reconnects automatically (server sends retry: 3000).
 */
export function useAlertStream() {
  const qc = useQueryClient()
  const lastToastAt = useRef(new Map<string, number>())

  useEffect(() => {
    const source = new EventSource("/api/v1/alerts/stream")

    function onAlertFired(e: MessageEvent) {
      let evt: AlertFiredEvent
      try {
        evt = JSON.parse(e.data as string) as AlertFiredEvent
      } catch {
        return
      }
      if (!evt?.alertId) return

      const now = Date.now()
      const last = lastToastAt.current.get(evt.alertId) ?? 0
      if (now - last >= TOAST_DEBOUNCE_MS) {
        lastToastAt.current.set(evt.alertId, now)
        toast.error(
          `Alert: ${evt.alertName} fired — ${evt.resultCount} result${evt.resultCount === 1 ? "" : "s"}`,
          { duration: TOAST_DURATION_MS },
        )
      }

      // Refresh the list (and with it the sidebar bell dot) on every firing,
      // debounced or not.
      qc.invalidateQueries({ queryKey: ["alerts"] })
    }

    source.addEventListener("alert-fired", onAlertFired)
    return () => {
      source.removeEventListener("alert-fired", onAlertFired)
      source.close()
    }
  }, [qc])
}
