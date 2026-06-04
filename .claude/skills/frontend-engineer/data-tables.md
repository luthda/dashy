# Data Table Patterns

Reference for the log table, expandable rows, loading skeletons, and empty states.

---

## Log Table

The primary data display in Dashy. Shows `LogEntry[]` from the query result with columns:
timestamp, level, message, source, and an expandable row for properties.

```typescript
// components/logs/LogTable.tsx
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table"
import type { LogEntry } from "@/types/api"

type LogTableProps = {
  entries: LogEntry[]
  isLoading: boolean
}

export function LogTable({ entries, isLoading }: LogTableProps) {
  if (isLoading) return <LogTableSkeleton />

  if (entries.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <p className="text-sm text-muted-foreground">
          No results — try widening the time range.
        </p>
      </div>
    )
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead className="w-[180px]">Timestamp</TableHead>
          <TableHead className="w-[80px]">Level</TableHead>
          <TableHead>Message</TableHead>
          <TableHead className="w-[120px]">Source</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {entries.map((entry, index) => (
          <LogRow key={index} entry={entry} />
        ))}
      </TableBody>
    </Table>
  )
}
```

---

## Expandable Log Rows

Each log row expands on click to reveal `eventType` and `properties` as a key-value list.

```typescript
// components/logs/LogRow.tsx
import { useState } from "react"
import { TableCell, TableRow } from "@/components/ui/table"
import { Badge } from "@/components/ui/badge"
import type { LogEntry } from "@/types/api"

export function LogRow({ entry }: { entry: LogEntry }) {
  const [expanded, setExpanded] = useState(false)

  return (
    <>
      <TableRow
        className="cursor-pointer hover:bg-muted/50"
        onClick={() => setExpanded(!expanded)}
      >
        <TableCell className="font-mono text-xs">
          {formatTimestamp(entry.timestamp)}
        </TableCell>
        <TableCell>
          <LevelBadge level={entry.level} />
        </TableCell>
        <TableCell className="max-w-[600px] truncate text-sm">
          {entry.message}
        </TableCell>
        <TableCell className="text-xs text-muted-foreground">
          {entry.source}
        </TableCell>
      </TableRow>

      {expanded && (
        <TableRow>
          <TableCell colSpan={4} className="bg-muted/30 p-4">
            {entry.eventType && (
              <div className="mb-2 text-sm">
                <span className="font-medium">EventType:</span> {entry.eventType}
              </div>
            )}
            <div className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-xs font-mono">
              {Object.entries(entry.properties).map(([key, value]) => (
                <Fragment key={key}>
                  <span className="text-muted-foreground">{key}</span>
                  <span>{value}</span>
                </Fragment>
              ))}
            </div>
          </TableCell>
        </TableRow>
      )}
    </>
  )
}
```

---

## Level Badge

Colour-coded badge for log levels. Keep level → colour mapping in one place.

```typescript
const LEVEL_COLORS: Record<string, string> = {
  error: "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200",
  warn: "bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200",
  info: "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200",
  debug: "bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200",
  trace: "bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400",
}

function LevelBadge({ level }: { level: string }) {
  return (
    <Badge variant="outline" className={cn("text-xs", LEVEL_COLORS[level])}>
      {level}
    </Badge>
  )
}
```

---

## Skeleton Loading

Show skeleton rows while a query is in flight. Match the table layout.

```typescript
function LogTableSkeleton() {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead className="w-[180px]">Timestamp</TableHead>
          <TableHead className="w-[80px]">Level</TableHead>
          <TableHead>Message</TableHead>
          <TableHead className="w-[120px]">Source</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {Array.from({ length: 8 }).map((_, i) => (
          <TableRow key={i}>
            <TableCell><Skeleton className="h-4 w-[140px]" /></TableCell>
            <TableCell><Skeleton className="h-4 w-[50px]" /></TableCell>
            <TableCell><Skeleton className="h-4 w-full" /></TableCell>
            <TableCell><Skeleton className="h-4 w-[80px]" /></TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
```

Rules:
- Skeleton row count: 8 rows (approximate viewport fill).
- Skeleton widths should approximate real content widths.
- Never show a spinner for table loading — use skeletons.

---

## Log Level Chart

Bar chart showing log counts bucketed by time, colour-coded by level. Uses Recharts.

```typescript
// components/logs/LogLevelChart.tsx
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer } from "recharts"

type ChartBucket = {
  time: string
  error: number
  warn: number
  info: number
  debug: number
  trace: number
}

type LogLevelChartProps = {
  buckets: ChartBucket[]
}

export function LogLevelChart({ buckets }: LogLevelChartProps) {
  if (buckets.length === 0) return null

  return (
    <ResponsiveContainer width="100%" height={120}>
      <BarChart data={buckets}>
        <XAxis dataKey="time" tick={{ fontSize: 10 }} />
        <YAxis width={40} tick={{ fontSize: 10 }} />
        <Tooltip />
        <Bar dataKey="error" stackId="a" fill="#ef4444" />
        <Bar dataKey="warn" stackId="a" fill="#eab308" />
        <Bar dataKey="info" stackId="a" fill="#3b82f6" />
        <Bar dataKey="debug" stackId="a" fill="#6b7280" />
        <Bar dataKey="trace" stackId="a" fill="#d1d5db" />
      </BarChart>
    </ResponsiveContainer>
  )
}
```

The bucket aggregation is done client-side from the `LogEntry[]` result. Keep the chart
compact — 120px height — so the table dominates.

---

## Alert & Source Lists

For simple entity lists (alerts, sources, saved searches), use shadcn `Card` rather than
`Table`. Each card shows a summary with action buttons.

```typescript
// Example: alert list item
<Card>
  <CardHeader className="flex flex-row items-center justify-between pb-2">
    <CardTitle className="text-sm font-medium">{alert.name}</CardTitle>
    <StatusBadge status={alert.status} />
  </CardHeader>
  <CardContent className="text-xs text-muted-foreground">
    <p>Query: {alert.query}</p>
    <p>Every {alert.checkIntervalSeconds}s · Threshold ≥ {alert.threshold}</p>
  </CardContent>
</Card>
```

---

## General Rules

1. **Loading** — always handle `isLoading`. Use skeletons for tables, spinners for buttons
   or small areas.
2. **Empty state** — every list has an explicit empty state with a call to action.
3. **Error state** — inline error banners for persistent errors (source unreachable), toasts
   for transient errors (mutation failed).
4. **No pagination for v1** — log queries have a `limit` parameter (default 500). The table
   renders all returned rows. Virtual scrolling may be added later.
5. **Timestamp formatting** — use a consistent helper (`formatTimestamp`) that renders
   ISO timestamps as `HH:mm:ss.SSS` for same-day, `MMM DD HH:mm:ss` otherwise.
