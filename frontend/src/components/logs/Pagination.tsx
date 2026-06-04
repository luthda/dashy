interface PaginationProps {
  page: number
  totalPages: number
  totalCount: number
  onPrev: () => void
  onNext: () => void
}

export function Pagination({ page, totalPages, totalCount, onPrev, onNext }: PaginationProps) {
  return (
    <div className="flex items-center justify-between px-1 text-[12px]">
      <span className="text-muted-foreground font-mono">
        {totalCount.toLocaleString()} total
      </span>
      <div className="flex items-center gap-2">
        <button
          disabled={page === 0}
          onClick={onPrev}
          className="border-border text-muted-foreground hover:text-foreground rounded-md border px-2.5 py-1 text-[11.5px] font-medium disabled:opacity-30"
        >
          Prev
        </button>
        <span className="text-muted-foreground font-mono text-[11.5px]">
          {page + 1} / {totalPages}
        </span>
        <button
          disabled={page >= totalPages - 1}
          onClick={onNext}
          className="border-border text-muted-foreground hover:text-foreground rounded-md border px-2.5 py-1 text-[11.5px] font-medium disabled:opacity-30"
        >
          Next
        </button>
      </div>
    </div>
  )
}
