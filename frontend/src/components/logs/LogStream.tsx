import { cn } from "@/lib/utils"
import { EVENT_TYPES, LEVELS, type LogEntry } from "@/lib/types"
import { ChevronRightIcon } from "lucide-react"
import { Fragment, useState } from "react"

interface LogStreamProps {
  rows: LogEntry[]
  isLoading: boolean
}

export function LogStream({ rows, isLoading }: LogStreamProps) {
  const [expanded, setExpanded] = useState<number | null>(null)

  if (isLoading && rows.length === 0) {
    return <LogStreamSkeleton />
  }

  return (
    <div className="bg-card border-border flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl border">
      <div className="text-muted-foreground border-border grid shrink-0 grid-cols-[18px_200px_72px_100px_144px_1fr] gap-3 border-b px-3.5 py-2.5 text-[11px] font-medium uppercase tracking-wider">
        <span />
        <span>Time</span>
        <span>Level</span>
        <span>Type</span>
        <span>Service</span>
        <span>Message</span>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {rows.length === 0 ? (
          <div className="text-muted-foreground py-12 text-center text-[13px]">
            No logs match your search.
          </div>
        ) : (
          rows.map((entry, i) => (
            <LogRow
              key={i}
              entry={entry}
              open={expanded === i}
              onToggle={() => setExpanded(expanded === i ? null : i)}
            />
          ))
        )}
      </div>

      <div className="text-muted-foreground border-border flex shrink-0 items-center justify-between border-t px-3.5 py-2 text-[11.5px]">
        <span className="font-mono">{rows.length} lines</span>
        <span>Click a line to expand</span>
      </div>
    </div>
  )
}

function LogRow({
  entry,
  open,
  onToggle,
}: {
  entry: LogEntry
  open: boolean
  onToggle: () => void
}) {
  const levelDef = LEVELS.find((l) => l.id === entry.level)
  const evtDef = EVENT_TYPES.find((e) => e.id === entry.eventType)
  const ts = new Date(entry.timestamp)
  const date = ts.toLocaleDateString(undefined, { year: "numeric", month: "2-digit", day: "2-digit" })
  const time = `${date} ${ts.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false, fractionalSecondDigits: 3 })}`
  const service = entry.properties?.["service"] ?? entry.source

  return (
    <>
      <div
        onClick={onToggle}
        className={cn(
          "border-border grid cursor-pointer grid-cols-[18px_200px_72px_100px_144px_1fr] gap-3 border-b px-3.5 py-1.5 text-[12.5px] transition-colors hover:bg-accent/50",
          open && "bg-accent/30",
        )}
      >
        <ChevronRightIcon
          size={12}
          className={cn(
            "text-muted-foreground mt-0.5 transition-transform",
            open && "rotate-90",
          )}
        />
        <span className="text-muted-foreground truncate font-mono text-[11.5px]">{time}</span>
        <span>
          <span
            className="inline-block rounded px-1.5 py-0.5 text-[10.5px] font-semibold uppercase"
            style={{
              color: levelDef?.color,
              background: levelDef ? `color-mix(in oklch, ${levelDef.color} 15%, transparent)` : undefined,
            }}
          >
            {entry.level}
          </span>
        </span>
        <span>
          {evtDef && (
            <span
              className="inline-block rounded px-1.5 py-0.5 text-[10.5px] font-medium"
              style={{
                color: evtDef.color,
                background: `color-mix(in oklch, ${evtDef.color} 12%, transparent)`,
              }}
            >
              {evtDef.label}
            </span>
          )}
        </span>
        <span className="text-muted-foreground truncate text-[12px]">{service}</span>
        <span className="truncate">{entry.message}</span>
      </div>

      {open && (
        <div className="border-border border-b bg-accent/20 px-10 py-3">
          <div className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-1 text-[12px]">
            <span className="text-muted-foreground font-medium">message</span>
            <span style={{ color: levelDef?.color }}>{entry.message}</span>
            <span className="text-muted-foreground font-medium">eventType</span>
            <span style={{ color: evtDef?.color }}>{evtDef?.label ?? entry.eventType}</span>
            {entry.properties.duration != null && (
              <>
                <span className="text-muted-foreground font-medium">duration</span>
                <span>{entry.properties.duration}</span>
              </>
            )}
            {entry.properties.resultCode != null && (
              <>
                <span className="text-muted-foreground font-medium">resultCode</span>
                <span>{entry.properties.resultCode}</span>
              </>
            )}
            {entry.properties.success != null && (
              <>
                <span className="text-muted-foreground font-medium">success</span>
                <span>{entry.properties.success}</span>
              </>
            )}
            {Object.entries(entry.properties)
              .filter(([k]) => !["duration", "resultCode", "success", "service"].includes(k))
              .map(([k, v]) => (
                <Fragment key={k}>
                  <span className="text-muted-foreground font-medium">{k}</span>
                  <span>{String(v)}</span>
                </Fragment>
              ))}
          </div>
        </div>
      )}
    </>
  )
}

function LogStreamSkeleton() {
  return (
    <div className="bg-card border-border flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl border">
      <div className="text-muted-foreground border-border grid shrink-0 grid-cols-[18px_200px_72px_100px_144px_1fr] gap-3 border-b px-3.5 py-2.5 text-[11px] font-medium uppercase tracking-wider">
        <span />
        <span>Time</span>
        <span>Level</span>
        <span>Type</span>
        <span>Service</span>
        <span>Message</span>
      </div>
      <div className="flex-1 overflow-hidden">
        {Array.from({ length: 8 }, (_, i) => (
          <div
            key={i}
            className="border-border grid grid-cols-[18px_200px_72px_100px_144px_1fr] gap-3 border-b px-3.5 py-2.5"
          >
            <span />
            <div className="bg-muted h-3.5 w-24 animate-pulse rounded" />
            <div className="bg-muted h-3.5 w-10 animate-pulse rounded" />
            <div className="bg-muted h-3.5 w-16 animate-pulse rounded" />
            <div className="bg-muted h-3.5 w-20 animate-pulse rounded" />
            <div className="bg-muted h-3.5 w-3/4 animate-pulse rounded" />
          </div>
        ))}
      </div>
    </div>
  )
}
