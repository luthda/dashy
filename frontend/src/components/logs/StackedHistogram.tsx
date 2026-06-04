export interface HistogramGroup {
  id: string
  label: string
  color: string
}

export interface StackedHistogramProps {
  data: Record<string, number>[]
  groups: HistogramGroup[]
  activeGroups: Set<string>
  height: number
}

export function StackedHistogram({ data, groups, activeGroups, height }: StackedHistogramProps) {
  const active = groups.filter((g) => activeGroups.has(g.id))

  const maxTotal = Math.max(
    1,
    ...data.map((b) => active.reduce((sum, g) => sum + (b[g.id] ?? 0), 0)),
  )

  return (
    <div className="flex items-end gap-px" style={{ height }}>
      {data.map((bucket, i) => {
        const total = active.reduce((sum, g) => sum + (bucket[g.id] ?? 0), 0)
        const barH = (total / maxTotal) * height

        return (
          <div key={i} className="flex flex-1 flex-col justify-end" style={{ height }}>
            {[...active].reverse().map((g) => {
              const count = bucket[g.id] ?? 0
              if (count === 0) return null
              const segH = total > 0 ? (count / total) * barH : 0
              return (
                <div
                  key={g.id}
                  className="w-full min-h-px rounded-[1px]"
                  style={{ height: segH, background: g.color }}
                />
              )
            })}
          </div>
        )
      })}
    </div>
  )
}
