import { cn } from "@/lib/utils"
import { RANGES, type Range } from "@/lib/types"
import { Loader2Icon, SearchIcon, XIcon } from "lucide-react"

interface SearchBarProps {
  query: string
  onQueryChange: (query: string) => void
  onSearch: () => void
  range: Range
  onRangeChange: (range: Range) => void
  live: boolean
  onLiveToggle: () => void
  onRefresh: () => void
  error?: string | null
  isLoading: boolean
  /** When the last successful query completed — manual or live. */
  lastUpdatedAt?: Date | null
}

export function SearchBar({
  query,
  onQueryChange,
  onSearch,
  range,
  onRangeChange,
  live,
  onLiveToggle,
  onRefresh,
  error,
  isLoading,
  lastUpdatedAt,
}: SearchBarProps) {
  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2.5">
        <label className="border-border bg-card flex min-w-60 flex-1 items-center gap-2.5 rounded-lg border px-3 h-9.5">
          <SearchIcon size={16} className="text-muted-foreground shrink-0" />
          <input
            value={query}
            onChange={(e) => onQueryChange(e.target.value)}
            onKeyDown={(e) => { if (e.key === "Enter") onSearch() }}
            spellCheck={false}
            placeholder="Search messages, services, hosts…"
            className="bg-transparent text-foreground w-full border-none text-[13.5px] outline-none placeholder:text-muted-foreground/60"
          />
          {query && (
            <button
              onClick={() => onQueryChange("")}
              className="text-muted-foreground hover:text-foreground"
            >
              <XIcon size={13} />
            </button>
          )}
        </label>

        <div className="bg-muted inline-flex gap-0.5 rounded-lg p-0.75">
          {RANGES.map((r) => (
            <button
              key={r}
              onClick={() => onRangeChange(r)}
              className={cn(
                "rounded-md px-2.5 py-1.5 font-mono text-xs font-medium",
                range === r
                  ? "bg-background text-foreground shadow-sm"
                  : "text-muted-foreground hover:text-foreground",
              )}
            >
              {r}
            </button>
          ))}
        </div>

        <div className="bg-muted inline-flex rounded-lg p-0.75">
          <button
            onClick={onLiveToggle}
            title={live ? "Live updates on — click to pause" : "Live updates paused — click to resume"}
            className={cn(
              "bg-background flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-medium shadow-sm",
              live ? "text-[var(--sev-success)]" : "text-[var(--sev-error)]",
            )}
          >
            <span className={live ? "live-dot" : "paused-dot"} />
            {live ? "Live" : "Paused"}
          </button>
        </div>

        <button
          onClick={onRefresh}
          disabled={isLoading}
          className="text-muted-foreground hover:text-foreground flex h-9.5 w-9.5 items-center justify-center rounded-lg border border-transparent"
        >
          {isLoading ? (
            <Loader2Icon size={16} className="animate-spin" />
          ) : (
            <RefreshIcon size={16} />
          )}
        </button>

        {lastUpdatedAt && (
          <span
            title="Time of the last log update — manual or live"
            className="text-muted-foreground font-mono text-[11.5px] whitespace-nowrap"
          >
            Updated {formatUpdatedAt(lastUpdatedAt)}
          </span>
        )}
      </div>

      {error && (
        <p className="text-[12px] text-[var(--sev-error)]">{error}</p>
      )}
    </div>
  )
}

function formatUpdatedAt(d: Date) {
  const date = d.toLocaleDateString(undefined, { day: "2-digit", month: "2-digit", year: "numeric" })
  const time = d.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit", second: "2-digit" })
  return `${date} ${time}`
}

function RefreshIcon({ size }: { size: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
      <path d="M3 3v5h5" />
      <path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16" />
      <path d="M16 21h5v-5" />
    </svg>
  )
}
