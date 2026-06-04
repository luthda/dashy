interface EmptyNoSourcesProps {
  onAdd: () => void
  children?: React.ReactNode
}

export function EmptyNoSources({ onAdd, children }: EmptyNoSourcesProps) {
  return (
    <div className="grid flex-1 place-items-center p-6">
      <div className="text-center">
        <div className="mb-1 text-[15px] font-semibold">No sources connected</div>
        <p className="text-muted-foreground mb-4 max-w-xs text-[12.5px]">
          Connect Azure App Insights or Grafana Loki to start querying logs.
        </p>
        <button
          onClick={onAdd}
          className="bg-primary text-primary-foreground h-9 rounded-md px-4 text-[13px] font-medium transition-opacity hover:opacity-90"
        >
          Connect a source
        </button>
      </div>
      {children}
    </div>
  )
}
