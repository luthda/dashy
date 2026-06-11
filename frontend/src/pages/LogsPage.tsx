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
import { useMemo, useState } from "react"

const DEFAULT_RANGE: Range = "1h"
const SOURCE_STORAGE_KEY = "dashy-source"

export function LogsPage() {
  const { data: sources } = useSourcesQuery()
  const { data: tags } = useTagsQuery()
  // The user's explicit selection, persisted across navigation and reloads.
  // Falls back to the first source when nothing is stored or the stored
  // source no longer exists.
  const [selectedSourceId, setSelectedSourceId] = useState(
    () => localStorage.getItem(SOURCE_STORAGE_KEY) ?? "",
  )
  const sourceId =
    (sources?.some((s) => s.id === selectedSourceId) ? selectedSourceId : sources?.[0]?.id) ?? ""
  const [query, setQuery] = useState("")
  // The last query actually submitted (Enter / saved search). The log query
  // re-runs on this, not the half-typed text in the search box.
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

  // Sorted arrays for a stable query key.
  const tagIds = useMemo(() => [...activeTagIds].sort(), [activeTagIds])
  const eventTypesFilter = useMemo(
    () =>
      activeEventTypes.size < EVENT_TYPES.length ? [...activeEventTypes].sort() : undefined,
    [activeEventTypes],
  )

  // Declarative: any change to these params refetches via the query key.
  const { data, hasMore, isLoading, isFetching, queryError, serverError, lastUpdatedAt, refetch } =
    useLogQuery({
      sourceId,
      query: submittedQuery,
      range,
      page,
      tagIds,
      eventTypes: eventTypesFilter,
      live,
    })

  function selectSource(id: string) {
    setSelectedSourceId(id)
    localStorage.setItem(SOURCE_STORAGE_KEY, id)
    setPage(0)
  }

  // Enter / search icon: submit the typed query; force a refetch when the
  // text didn't change (the key would otherwise consider the data fresh).
  function submitSearch() {
    if (query === submittedQuery) {
      refetch()
    } else {
      setSubmittedQuery(query)
      setPage(0)
    }
  }

  // Load a saved search string into the bar and run it via the key change.
  function applySavedSearch(q: string) {
    setQuery(q)
    setSubmittedQuery(q)
    setPage(0)
  }

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
            onSearch={submitSearch}
            range={range}
            onRangeChange={(r) => { setRange(r); setPage(0) }}
            live={live}
            onLiveToggle={() => setLive((v) => !v)}
            onRefresh={() => refetch()}
            error={queryError ?? serverError}
            isLoading={isFetching}
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
          onToggle={(id) => { toggleSet(setActiveTagIds, id); setPage(0) }}
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
            onChange={(e) => selectSource(e.target.value)}
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
        onToggleEventType={(id) => { toggleSet(setActiveEventTypes, id); setPage(0) }}
      />

      <LogStream rows={filteredRows} isLoading={isLoading} />

      {(page > 0 || hasMore) && (
        <Pagination
          page={page}
          hasMore={hasMore}
          onPrev={() => setPage(page - 1)}
          onNext={() => setPage(page + 1)}
        />
      )}
    </div>
  )
}
