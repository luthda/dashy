// ── Backend response types ──────────────────────────────────────────────────

export const SourceType = {
  AppInsights: "AppInsights",
  Loki: "Loki",
} as const

export type SourceType = (typeof SourceType)[keyof typeof SourceType]

export interface Source {
  id: string
  name: string
  type: SourceType
  createdAt: string
}

export interface ConnectionTestResult {
  ok: boolean
  error?: string
}

export interface LogEntry {
  timestamp: string
  level: "error" | "warn" | "info" | "debug" | "trace"
  message: string
  source: string
  eventType?: string
  properties: Record<string, string>
}

export interface LogQueryRequest {
  sourceId: string
  query?: string
  tagIds?: string[]
  timeRange?: TimeRange
  limit?: number
}

export interface TimeRange {
  type: "relative" | "absolute"
  value?: string // "15m" | "1h" | "6h" | "24h" | "7d"
  from?: string // ISO 8601 for absolute
  to?: string
}

// ── Form / request types ────────────────────────────────────────────────────

export interface AppInsightsConfig {
  appId: string
  apiKey: string
}

export interface LokiConfig {
  baseUrl: string
  orgId?: string
  authToken?: string
}

export type SourceConfig = AppInsightsConfig | LokiConfig

export const LEVELS = [
  { id: "error" as const, label: "Error", color: "var(--sev-error)" },
  { id: "warn" as const, label: "Warn", color: "var(--sev-warn)" },
  { id: "info" as const, label: "Info", color: "var(--sev-info)" },
  { id: "debug" as const, label: "Debug", color: "var(--sev-debug)" },
  { id: "trace" as const, label: "Trace", color: "var(--sev-trace)" },
] as const

export type Level = (typeof LEVELS)[number]["id"]

export const RANGES = ["15m", "1h", "6h", "24h", "7d"] as const
export type Range = (typeof RANGES)[number]
