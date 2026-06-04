import { SearchBar } from "@/components/logs/SearchBar"
import { StackedHistogram, type BucketRow } from "@/components/logs/StackedHistogram"
import { LogStream } from "@/components/logs/LogStream"
import { SourceSetupDialog } from "@/components/sources/SourceSetupDialog"
import { useLogQuery } from "@/hooks/useLogQuery"
import { useSourcesQuery } from "@/hooks/useSources"
import { LEVELS, type Range } from "@/lib/types"
import { cn } from "@/lib/utils"
import { useEffect, useMemo, useRef, useState } from "react"

const DEFAULT_RANGE: Range = "1h"
const LIVE_INTERVAL_MS = 30_000

export function LogsPage() {
  const { data: sources } = useSourcesQuery()

  const [sourceId, setSourceId]       = useState<string>("")
  const [query, setQuery]             = useState("")
  const [range, setRange]             = useState<Range>(DEFAULT_RANGE)
  const [live, setLive]               = useState(false)
  const [activeLevels, setActiveLevels] = useState<Set<string>>(
    () => new Set(LEVELS.map((l) => l.id)),
  )
  const [showAddSource, setShowAddSource] = useState(false)

  const { data, isLoading, queryError, serverError, run } = useLogQuery()

  // Auto-select first source
  useEffect(() => {
    if (sources?.length && !sourceId) {
      setSourceId(sources[0].id)
    }
  }, [sources, sourceId])

  // Run on initial load and when sourceId/range changes
  const prevSourceId = useRef(sourceId)
  const prevRange    = useRef(range)
  useEffect(() => {
    if (!sourceId) return
    if (prevSourceId.current !== sourceId || prevRange.current !== range) {
      prevSourceId.current = sourceId
      prevRange.current    = range
      runQuery()
    }
  })

  // Live mode polling
  useEffect(() => {
    if (!live || !sourceId) return
    const id = setInterval(runQuery, LIVE_INTERVAL_MS)
    return () => clearInterval(id)
  }, [live, sourceId, query, range])

  function runQuery() {
    if (!sourceId) return
    run({
      sourceId,
      query: query || undefined,
      timeRange: { type: "relative", value: range },
      limit: 500,
    })
  }

  function toggleLevel(id: string) {
    setActiveLevels((prev) => {
      const n = new Set(prev)
      n.has(id) ? n.delete(id) : n.add(id)
      return n
    })
  }

  // Filter displayed rows by active levels
  const filteredRows = useMemo(
    () => (data ?? []).filter((e) => activeLevels.has(e.level)),
    [data, activeLevels],
  )

  // Build histogram buckets from log entries
  const histogramData = useMemo(
    () => buildHistogram(data ?? [], 40),
    [data],
  )

  if (!sources?.length) {
    return (
      <EmptyNoSources onAdd={() => setShowAddSource(true)}>
        {showAddSource && <SourceSetupDialog onClose={() => setShowAddSource(false)} />}
      </EmptyNoSources>
    )
  }

  return (
    <div className="flex flex-col h-full min-h-0 p-[18px_22px] gap-3">
      {/* Controls row */}
      <SearchBar
        query={query}
        onQueryChange={setQuery}
        range={range}
        onRangeChange={(r) => { setRange(r); runQuery() }}
        live={live}
        onLiveToggle={() => setLive((v) => !v)}
        onRefresh={runQuery}
        error={queryError ?? serverError}
        isLoading={isLoading}
      />

      {/* Source selector (if multiple sources) */}
      {sources.length > 1 && (
        <div className="flex items-center gap-2">
          <span className="text-[12px] text-muted-foreground">Source:</span>
          <select
            value={sourceId}
            onChange={(e) => setSourceId(e.target.value)}
            className="h-8 px-2 rounded-md border border-border bg-background text-[12.5px] outline-none"
          >
            {sources.map((s) => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        </div>
      )}

      {/* Histogram + level filters */}
      <div className="bg-card border border-border rounded-xl px-3.5 pt-3 pb-2">
        <div className="flex items-center justify-between mb-1.5 flex-wrap gap-2">
          <span className="text-[12.5px] text-muted-foreground">
            <span className="font-mono font-semibold text-foreground">
              {filteredRows.length.toLocaleString()}
            </span>{" "}
            events shown · last {range}
          </span>

          {/* Level filter toggles */}
          <div className="flex gap-1.5 flex-wrap">
            {LEVELS.map((l) => {
              const on = activeLevels.has(l.id)
              return (
                <button
                  key={l.id}
                  onClick={() => toggleLevel(l.id)}
                  className={cn(
                    "flex items-center gap-1.5 h-6 px-2 rounded-full border text-[11.5px] font-medium transition-all",
                    on ? "border-border bg-card" : "border-transparent opacity-40",
                  )}
                >
                  <span
                    className="w-2 h-2 rounded-sm"
                    style={{ background: l.color }}
                  />
                  {l.label}
                </button>
              )
            })}
          </div>
        </div>

        <StackedHistogram
          data={histogramData}
          activeLevels={activeLevels}
          height={78}
        />
      </div>

      {/* Log stream */}
      <LogStream rows={filteredRows} isLoading={isLoading} />
    </div>
  )
}

function EmptyNoSources({
  onAdd,
  children,
}: {
  onAdd: () => void
  children?: React.ReactNode
}) {
  return (
    <div className="flex-1 grid place-items-center p-6">
      <div className="text-center">
        <div className="text-[15px] font-semibold mb-1">No sources connected</div>
        <p className="text-[12.5px] text-muted-foreground mb-4 max-w-xs">
          Connect Azure App Insights or Grafana Loki to start querying logs.
        </p>
        <button
          onClick={onAdd}
          className="h-9 px-4 rounded-md bg-primary text-primary-foreground text-[13px] font-medium hover:opacity-90 transition-opacity"
        >
          Connect a source
        </button>
      </div>
      {children}
    </div>
  )
}

// ── Histogram builder ──────────────────────────────────────────────────────────

function buildHistogram(entries: import("@/lib/types").LogEntry[], buckets: number): BucketRow[] {
  if (entries.length === 0) {
    return Array.from({ length: buckets }, () => ({
      error: 0, warn: 0, info: 0, debug: 0, trace: 0,
    }))
  }

  const times = entries.map((e) => new Date(e.timestamp).getTime())
  const min = Math.min(...times)
  const max = Math.max(...times)
  const range = max - min || 1

  const result: BucketRow[] = Array.from({ length: buckets }, () => ({
    error: 0, warn: 0, info: 0, debug: 0, trace: 0,
  }))

  for (const entry of entries) {
    const t = new Date(entry.timestamp).getTime()
    const i = Math.min(
      buckets - 1,
      Math.floor(((t - min) / range) * buckets),
    )
    const row = result[i]!
    const level = entry.level as keyof BucketRow
    if (level in row) row[level]++
  }

  return result
}
