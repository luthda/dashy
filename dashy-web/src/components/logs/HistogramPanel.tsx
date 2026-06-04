import { StackedHistogram, type HistogramGroup } from "@/components/logs/StackedHistogram"
import { FilterChips } from "@/components/logs/FilterChips"
import { LEVELS, EVENT_TYPES, type LogEntry, type Range } from "@/lib/types"
import { cn } from "@/lib/utils"
import { useMemo } from "react"

type GroupBy = "level" | "eventType"

interface HistogramPanelProps {
  entries: LogEntry[]
  filteredCount: number
  range: Range
  groupBy: GroupBy
  onGroupByChange: (g: GroupBy) => void
  activeLevels: Set<string>
  onToggleLevel: (id: string) => void
  activeEventTypes: Set<string>
  onToggleEventType: (id: string) => void
}

const LEVEL_GROUPS: HistogramGroup[] = LEVELS.map((l) => ({ ...l }))
const EVENT_TYPE_GROUPS: HistogramGroup[] = EVENT_TYPES.map((e) => ({ ...e }))

export function HistogramPanel({
  entries,
  filteredCount,
  range,
  groupBy,
  onGroupByChange,
  activeLevels,
  onToggleLevel,
  activeEventTypes,
  onToggleEventType,
}: HistogramPanelProps) {
  const groups = groupBy === "level" ? LEVEL_GROUPS : EVENT_TYPE_GROUPS
  const activeSet = groupBy === "level" ? activeLevels : activeEventTypes

  const buckets = useMemo(
    () => buildHistogram(entries, 40, groupBy),
    [entries, groupBy],
  )

  const eventTypeCounts = useMemo(() => {
    const counts = new Map<string, number>()
    for (const entry of entries) {
      const t = entry.eventType
      counts.set(t, (counts.get(t) ?? 0) + 1)
    }
    return counts
  }, [entries])

  return (
    <div className="bg-card border-border rounded-xl border px-3.5 pt-3 pb-2">
      {/* Top: count + group toggle + level chips */}
      <div className="mb-1.5 flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-3">
          <span className="text-[12.5px] text-muted-foreground">
            <span className="font-mono font-semibold text-foreground">
              {filteredCount.toLocaleString()}
            </span>{" "}
            events shown · last {range}
          </span>
          <div className="bg-muted inline-flex gap-0.5 rounded-md p-[2px]">
            {(["level", "eventType"] as const).map((g) => (
              <button
                key={g}
                onClick={() => onGroupByChange(g)}
                className={cn(
                  "rounded px-2 py-0.5 text-[10.5px] font-medium transition-colors",
                  groupBy === g
                    ? "bg-background text-foreground shadow-sm"
                    : "text-muted-foreground hover:text-foreground",
                )}
              >
                {g === "level" ? "Level" : "Type"}
              </button>
            ))}
          </div>
        </div>
        <FilterChips
          chips={LEVELS}
          active={activeLevels}
          onToggle={onToggleLevel}
        />
      </div>

      {/* Event type chips row */}
      <div className="mb-2 flex flex-wrap items-center gap-2">
        <FilterChips
          chips={EVENT_TYPES}
          active={activeEventTypes}
          onToggle={onToggleEventType}
          counts={eventTypeCounts}
        />
      </div>

      <StackedHistogram data={buckets} groups={groups} activeGroups={activeSet} height={78} />
    </div>
  )
}

// ── Histogram builder ──────────────────────────────────────────────────────────

function buildHistogram(
  entries: LogEntry[],
  numBuckets: number,
  groupBy: GroupBy,
): Record<string, number>[] {
  const emptyBucket = (): Record<string, number> => {
    const b: Record<string, number> = {}
    const defs = groupBy === "level" ? LEVELS : EVENT_TYPES
    for (const d of defs) b[d.id] = 0
    return b
  }

  if (entries.length === 0) {
    return Array.from({ length: numBuckets }, emptyBucket)
  }

  const times = entries.map((e) => new Date(e.timestamp).getTime())
  const min = Math.min(...times)
  const max = Math.max(...times)
  const span = max - min || 1

  const result = Array.from({ length: numBuckets }, emptyBucket)

  for (const entry of entries) {
    const t = new Date(entry.timestamp).getTime()
    const i = Math.min(numBuckets - 1, Math.floor(((t - min) / span) * numBuckets))
    const row = result[i]!
    const key = groupBy === "level" ? entry.level : entry.eventType
    if (key in row) row[key]++
  }

  return result
}
