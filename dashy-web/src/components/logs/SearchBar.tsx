import { cn } from "@/lib/utils"
import { RANGES, type Range } from "@/lib/types"
import { Loader2Icon, SearchIcon, XIcon } from "lucide-react"

interface SearchBarProps {
  query: string
  onQueryChange: (query: string) => void
  range: Range
  onRangeChange: (range: Range) => void
  live: boolean
  onLiveToggle: () => void
  onRefresh: () => void
  error?: string | null
  isLoading: boolean
}

export function SearchBar({
  query,
  onQueryChange,
  range,
  onRangeChange,
  live,
  onLiveToggle,
  onRefresh,
  error,
  isLoading,
}: SearchBarProps) {
  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2.5">
        <label className="border-border bg-card flex min-w-60 flex-1 items-center gap-2.5 rounded-lg border px-3 h-[38px]">
          <SearchIcon size={16} className="text-muted-foreground shrink-0" />
          <input
            value={query}
            onChange={(e) => onQueryChange(e.target.value)}
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

        <div className="bg-muted inline-flex gap-0.5 rounded-lg p-[3px]">
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

        <button
          onClick={onLiveToggle}
          className={cn(
            "flex h-[38px] items-center gap-1.5 rounded-lg border px-3 text-[12.5px] font-medium",
            live
              ? "border-[color-mix(in_oklch,var(--sev-success)_35%,transparent)] text-[var(--sev-success)]"
              : "border-border text-muted-foreground",
          )}
        >
          {live ? <span className="live-dot" /> : null}
          {live ? "Live" : "Paused"}
        </button>

        <button
          onClick={onRefresh}
          disabled={isLoading}
          className="text-muted-foreground hover:text-foreground flex h-[38px] w-[38px] items-center justify-center rounded-lg border border-transparent"
        >
          {isLoading ? (
            <Loader2Icon size={16} className="animate-spin" />
          ) : (
            <RefreshIcon size={16} />
          )}
        </button>
      </div>

      {error && (
        <p className="text-[12px] text-[var(--sev-error)]">{error}</p>
      )}
    </div>
  )
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
