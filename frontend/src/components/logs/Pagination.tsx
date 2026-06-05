interface PaginationProps {
  page: number
  hasMore: boolean
  onPrev: () => void
  onNext: () => void
}

export function Pagination({ page, hasMore, onPrev, onNext }: PaginationProps) {
  return (
    <div className="flex items-center justify-end px-1 text-[12px]">
      <div className="flex items-center gap-2">
        <button
          disabled={page === 0}
          onClick={onPrev}
          className="border-border text-muted-foreground hover:text-foreground rounded-md border px-2.5 py-1 text-[11.5px] font-medium disabled:opacity-30"
        >
          Prev
        </button>
        <span className="text-muted-foreground font-mono text-[11.5px]">
          Page {page + 1}
        </span>
        <button
          disabled={!hasMore}
          onClick={onNext}
          className="border-border text-muted-foreground hover:text-foreground rounded-md border px-2.5 py-1 text-[11.5px] font-medium disabled:opacity-30"
        >
          Next
        </button>
      </div>
    </div>
  )
}
