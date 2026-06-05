// ── Backend response types ──────────────────────────────────────────────────

export const SourceType = {
  AppInsights: "AppInsights",
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
  eventType: string
  properties: Record<string, string>
}

export interface LogQueryRequest {
  sourceId: string
  query?: string
  tagIds?: string[]
  eventTypes?: string[]
  timeRange?: TimeRange
  limit?: number
  skip?: number
}

export interface TimeRange {
  type: "relative" | "absolute"
  value?: string // "15m" | "1h" | "6h" | "24h" | "7d"
  from?: string // ISO 8601 for absolute
  to?: string
}

export interface Tag {
  id: string
  name: string
  color: string
  filters: TagFilters
  createdAt: string
}

export interface TagFilters {
  terms: string[]
  levels: string[]
  eventTypes: string[]
}

export interface SavedSearch {
  id: string
  name: string
  query: string
  createdAt: string
}

// ── Form / request types ────────────────────────────────────────────────────

export interface AppInsightsConfig {
  appId: string
  apiKey: string
}

export type SourceConfig = AppInsightsConfig

export const LEVELS = [
  { id: "error" as const, label: "Error", color: "var(--sev-error)" },
  { id: "warn" as const, label: "Warn", color: "var(--sev-warn)" },
  { id: "info" as const, label: "Info", color: "var(--sev-info)" },
  { id: "debug" as const, label: "Debug", color: "var(--sev-debug)" },
  { id: "trace" as const, label: "Trace", color: "var(--sev-trace)" },
] as const

export type Level = (typeof LEVELS)[number]["id"]

export const EVENT_TYPES = [
  { id: "trace" as const, label: "Trace", color: "var(--evt-trace)" },
  { id: "request" as const, label: "Request", color: "var(--evt-request)" },
  { id: "dependency" as const, label: "Dependency", color: "var(--evt-dependency)" },
  { id: "exception" as const, label: "Exception", color: "var(--evt-exception)" },
  { id: "customEvent" as const, label: "Custom Event", color: "var(--evt-custom)" },
  { id: "availability" as const, label: "Availability", color: "var(--evt-availability)" },
  { id: "pageView" as const, label: "Page View", color: "var(--evt-pageview)" },
] as const

export type EventTypeId = (typeof EVENT_TYPES)[number]["id"]

export const RANGES = ["15m", "1h", "6h", "24h", "7d"] as const
export type Range = (typeof RANGES)[number]
