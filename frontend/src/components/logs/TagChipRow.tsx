import { cn } from "@/lib/utils"
import type { Tag } from "@/lib/types"

interface TagChipRowProps {
  tags: Tag[]
  activeTagIds: Set<string>
  onToggle: (tagId: string) => void
}

export function TagChipRow({ tags, activeTagIds, onToggle }: TagChipRowProps) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {tags.map((tag) => {
        const active = activeTagIds.has(tag.id)
        return (
          <button
            key={tag.id}
            onClick={() => onToggle(tag.id)}
            className={cn(
              "flex items-center gap-1.5 h-6 px-2.5 rounded-full border text-[11.5px] font-medium transition-all",
              active ? "border-border bg-card" : "border-transparent opacity-40",
            )}
          >
            <span
              className="h-2 w-2 rounded-sm"
              style={{ background: tag.color }}
            />
            {tag.name}
          </button>
        )
      })}
    </div>
  )
}
