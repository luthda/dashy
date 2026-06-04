import { cn } from "@/lib/utils"

export interface ChipDef {
  id: string
  label: string
  color: string
}

interface FilterChipsProps {
  chips: readonly ChipDef[]
  active: Set<string>
  onToggle: (id: string) => void
  counts?: Map<string, number>
}

function formatCount(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(2)}M`
  if (n >= 1_000) return `${(n / 1_000).toFixed(2)}k`
  return String(n)
}

export function FilterChips({ chips, active, onToggle, counts }: FilterChipsProps) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {chips.map((c) => {
        const on = active.has(c.id)
        const count = counts?.get(c.id)
        return (
          <button
            key={c.id}
            onClick={() => onToggle(c.id)}
            className={cn(
              "flex items-center gap-1.5 h-6 px-2 rounded-full border text-[11.5px] font-medium transition-all",
              on ? "border-border bg-card" : "border-transparent opacity-40",
            )}
          >
            <span className="w-2 h-2 rounded-sm" style={{ background: c.color }} />
            {c.label}
            {count != null && (
              <span className="text-muted-foreground ml-0.5 font-mono text-[10px]">
                {formatCount(count)}
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}
