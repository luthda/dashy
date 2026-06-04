import { EmptyNoSources } from "@/components/logs/EmptyNoSources"
import { HistogramPanel } from "@/components/logs/HistogramPanel"
import { LogStream } from "@/components/logs/LogStream"
import { Pagination } from "@/components/logs/Pagination"
import { SearchBar } from "@/components/logs/SearchBar"
import { SourceSetupDialog } from "@/components/sources/SourceSetupDialog"
import { useLogQuery } from "@/hooks/useLogQuery"
import { useSourcesQuery } from "@/hooks/useSources"
import { EVENT_TYPES, LEVELS, type Range } from "@/lib/types"
import { useCallback, useEffect, useMemo, useRef, useState } from "react"

const DEFAULT_RANGE: Range = "1h"
const LIVE_MS = 30_000
const PAGE = 500

export function LogsPage() {
  const { data: sources } = useSourcesQuery()
  const [sourceId, setSourceId] = useState("")
  const [query, setQuery] = useState("")
  const [range, setRange] = useState<Range>(DEFAULT_RANGE)
  const [live, setLive] = useState(false)
  const [activeLevels, setActiveLevels] = useState<Set<string>>(
    () => new Set(LEVELS.map((l) => l.id)),
  )
  const [activeEventTypes, setActiveEventTypes] = useState<Set<string>>(
    () => new Set(EVENT_TYPES.map((e) => e.id)),
  )
  const [groupBy, setGroupBy] = useState<"level" | "eventType">("level")
  const [page, setPage] = useState(0)
  const [showAddSource, setShowAddSource] = useState(false)
  const { data, totalCount, isLoading, queryError, serverError, run } = useLogQuery()

  const runQuery = useCallback(
    (p: number = page) => {
      if (!sourceId) return
      const evtFilter = activeEventTypes.size < EVENT_TYPES.length ? [...activeEventTypes] : undefined
      run({
        sourceId, query: query || undefined, eventTypes: evtFilter,
        timeRange: { type: "relative", value: range }, limit: PAGE, skip: p * PAGE,
      })
    },
    [sourceId, query, range, activeEventTypes, page, run],
  )

  useEffect(() => { if (sources?.length && !sourceId) setSourceId(sources[0].id) }, [sources, sourceId])

  const prevSourceId = useRef(sourceId)
  const prevRange = useRef(range)
  useEffect(() => {
    if (!sourceId) return
    if (prevSourceId.current !== sourceId || prevRange.current !== range) {
      prevSourceId.current = sourceId; prevRange.current = range; setPage(0); runQuery(0)
    }
  })

  useEffect(() => {
    if (!live || !sourceId) return
    const id = setInterval(() => runQuery(page), LIVE_MS)
    return () => clearInterval(id)
  }, [live, sourceId, runQuery, page])

  function toggleSet(setter: React.Dispatch<React.SetStateAction<Set<string>>>, id: string) {
    setter((prev) => {
      const n = new Set(prev)
      n.has(id) ? n.delete(id) : n.add(id)
      return n
    })
  }

  const filteredRows = useMemo(
    () =>
      (data ?? []).filter(
        (e) => activeLevels.has(e.level) && activeEventTypes.has(e.eventType),
      ),
    [data, activeLevels, activeEventTypes],
  )

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE))

  if (!sources?.length) {
    return (
      <EmptyNoSources onAdd={() => setShowAddSource(true)}>
        {showAddSource && <SourceSetupDialog onClose={() => setShowAddSource(false)} />}
      </EmptyNoSources>
    )
  }

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 p-[18px_22px]">
      <SearchBar
        query={query}
        onQueryChange={setQuery}
        range={range}
        onRangeChange={(r) => { setRange(r); setPage(0) }}
        live={live}
        onLiveToggle={() => setLive((v) => !v)}
        onRefresh={() => runQuery(page)}
        error={queryError ?? serverError}
        isLoading={isLoading}
      />

      {sources.length > 1 && (
        <div className="flex items-center gap-2">
          <span className="text-muted-foreground text-[12px]">Source:</span>
          <select
            value={sourceId}
            onChange={(e) => setSourceId(e.target.value)}
            className="border-border bg-background h-8 rounded-md border px-2 text-[12.5px] outline-none"
          >
            {sources.map((s) => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        </div>
      )}

      <HistogramPanel
        entries={data ?? []}
        filteredCount={filteredRows.length}
        range={range}
        groupBy={groupBy}
        onGroupByChange={setGroupBy}
        activeLevels={activeLevels}
        onToggleLevel={(id) => toggleSet(setActiveLevels, id)}
        activeEventTypes={activeEventTypes}
        onToggleEventType={(id) => toggleSet(setActiveEventTypes, id)}
      />

      <LogStream rows={filteredRows} isLoading={isLoading} />

      {totalCount > PAGE && (
        <Pagination
          page={page}
          totalPages={totalPages}
          totalCount={totalCount}
          onPrev={() => { const p = page - 1; setPage(p); runQuery(p) }}
          onNext={() => { const p = page + 1; setPage(p); runQuery(p) }}
        />
      )}
    </div>
  )
}
