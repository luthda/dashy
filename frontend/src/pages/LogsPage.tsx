import { EmptyNoSources } from "@/components/logs/EmptyNoSources"
import { HistogramPanel } from "@/components/logs/HistogramPanel"
import { LogStream } from "@/components/logs/LogStream"
import { Pagination } from "@/components/logs/Pagination"
import { SearchBar } from "@/components/logs/SearchBar"
import { TagChipRow } from "@/components/logs/TagChipRow"
import { SavedSearchesDialog } from "@/components/searches/SavedSearchesDialog"
import { SourceSetupDialog } from "@/components/sources/SourceSetupDialog"
import { TagsDialog } from "@/components/tags/TagsDialog"
import { useLogQuery } from "@/hooks/useLogQuery"
import { useSourcesQuery } from "@/hooks/useSources"
import { useTagsQuery } from "@/hooks/useTags"
import { EVENT_TYPES, LEVELS, type Range } from "@/lib/types"
import { BookmarkIcon, TagIcon } from "lucide-react"
import { useCallback, useEffect, useMemo, useRef, useState } from "react"

const DEFAULT_RANGE: Range = "1h"
const LIVE_MS = 60_000
const PAGE = 500

export function LogsPage() {
  const { data: sources } = useSourcesQuery()
  const { data: tags } = useTagsQuery()
  // The user's explicit selection; falls back to the first source once loaded.
  const [selectedSourceId, setSelectedSourceId] = useState("")
  const sourceId = selectedSourceId || sources?.[0]?.id || ""
  const [query, setQuery] = useState("")
  // The last query actually submitted (Enter / saved search). Live ticks and
  // pagination re-run this, not the half-typed text in the search box.
  const [submittedQuery, setSubmittedQuery] = useState("")
  const [range, setRange] = useState<Range>(DEFAULT_RANGE)
  const [live, setLive] = useState(true)
  const [activeLevels, setActiveLevels] = useState<Set<string>>(
    () => new Set(LEVELS.map((l) => l.id)),
  )
  const [activeEventTypes, setActiveEventTypes] = useState<Set<string>>(
    () => new Set(EVENT_TYPES.map((e) => e.id)),
  )
  const [activeTagIds, setActiveTagIds] = useState<Set<string>>(new Set())
  const [groupBy, setGroupBy] = useState<"level" | "eventType">("level")
  const [page, setPage] = useState(0)
  const [showAddSource, setShowAddSource] = useState(false)
  const [showTagsDialog, setShowTagsDialog] = useState(false)
  const [showSavedSearches, setShowSavedSearches] = useState(false)
  const { data, hasMore, isLoading, queryError, serverError, lastUpdatedAt, run } = useLogQuery()

  const runQuery = useCallback(
    (p: number = page, q: string = submittedQuery) => {
      if (!sourceId) return
      const evtFilter = activeEventTypes.size < EVENT_TYPES.length ? [...activeEventTypes] : undefined
      const tagIds = activeTagIds.size > 0 ? [...activeTagIds] : undefined
      run({
        sourceId, query: q || undefined, eventTypes: evtFilter, tagIds,
        timeRange: { type: "relative", value: range }, limit: PAGE, skip: p * PAGE,
      })
    },
    [sourceId, range, activeEventTypes, activeTagIds, page, submittedQuery, run],
  )

  // Load a saved search string into the bar and run it immediately. `query` state
  // hasn't flushed yet this tick, so pass the new string to runQuery explicitly.
  const applySavedSearch = useCallback(
    (q: string) => {
      setQuery(q)
      setSubmittedQuery(q)
      setPage(0)
      runQuery(0, q)
    },
    [runQuery],
  )

  // Ref-tracking: compares refs to detect changes and auto-requery.
  // prevSourceId starts at "" (not sourceId) so the first render with a real
  // source always triggers the initial query — even when sources come from the
  // TanStack Query cache and are available synchronously on mount.
  const prevSourceId = useRef("")
  const prevRange = useRef(range)
  const prevTagIds = useRef(activeTagIds)
  useEffect(() => {
    if (!sourceId) return
    if (prevSourceId.current !== sourceId || prevRange.current !== range || prevTagIds.current !== activeTagIds) {
      prevSourceId.current = sourceId; prevRange.current = range; prevTagIds.current = activeTagIds; setPage(0); runQuery(0)
    }
  }, [sourceId, range, activeTagIds, runQuery])

  // Latest runQuery in a ref so the polling interval isn't torn down and
  // restarted every time runQuery's identity changes (e.g. on each keystroke).
  const runQueryRef = useRef(runQuery)
  useEffect(() => {
    runQueryRef.current = runQuery
  }, [runQuery])

  useEffect(() => {
    if (!live || !sourceId) return
    const id = setInterval(() => runQueryRef.current(), LIVE_MS)
    return () => clearInterval(id)
  }, [live, sourceId])

  function toggleSet(setter: React.Dispatch<React.SetStateAction<Set<string>>>, id: string) {
    setter((prev) => {
      const n = new Set(prev)
      if (n.has(id)) n.delete(id)
      else n.add(id)
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

  if (!sources?.length) {
    return (
      <EmptyNoSources onAdd={() => setShowAddSource(true)}>
        {showAddSource && <SourceSetupDialog onClose={() => setShowAddSource(false)} />}
      </EmptyNoSources>
    )
  }

  return (
    <div className="flex h-full min-h-0 flex-col gap-3 px-5.5 py-4.5">
      <div className="flex items-start gap-2">
        <div className="min-w-0 flex-1">
          <SearchBar
            query={query}
            onQueryChange={setQuery}
            onSearch={() => { setSubmittedQuery(query); setPage(0); runQuery(0, query) }}
            range={range}
            onRangeChange={(r) => { setRange(r); setPage(0) }}
            live={live}
            onLiveToggle={() => setLive((v) => !v)}
            onRefresh={() => runQuery(page)}
            error={queryError ?? serverError}
            isLoading={isLoading}
            lastUpdatedAt={lastUpdatedAt}
          />
        </div>
        <button
          onClick={() => setShowSavedSearches(true)}
          title="Saved searches"
          className="text-muted-foreground hover:text-foreground hover:border-border flex h-9.5 w-9.5 shrink-0 items-center justify-center rounded-lg border border-transparent"
        >
          <BookmarkIcon size={16} />
        </button>
        <button
          onClick={() => setShowTagsDialog(true)}
          title="Manage tags"
          className="text-muted-foreground hover:text-foreground hover:border-border flex h-9.5 w-9.5 shrink-0 items-center justify-center rounded-lg border border-transparent"
        >
          <TagIcon size={16} />
        </button>
      </div>

      {(tags ?? []).length > 0 && (
        <TagChipRow
          tags={tags ?? []}
          activeTagIds={activeTagIds}
          onToggle={(id) => toggleSet(setActiveTagIds, id)}
        />
      )}

      {showTagsDialog && <TagsDialog onClose={() => setShowTagsDialog(false)} />}

      {showSavedSearches && (
        <SavedSearchesDialog
          currentQuery={query}
          onApply={applySavedSearch}
          onClose={() => setShowSavedSearches(false)}
        />
      )}

      {sources.length > 1 && (
        <div className="flex items-center gap-2">
          <span className="text-muted-foreground text-[12px]">Source:</span>
          <select
            value={sourceId}
            onChange={(e) => setSelectedSourceId(e.target.value)}
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

      {(page > 0 || hasMore) && (
        <Pagination
          page={page}
          hasMore={hasMore}
          onPrev={() => { const p = page - 1; setPage(p); runQuery(p) }}
          onNext={() => { const p = page + 1; setPage(p); runQuery(p) }}
        />
      )}
    </div>
  )
}
