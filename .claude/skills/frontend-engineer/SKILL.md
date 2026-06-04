---
name: frontend-engineer
description: Use when implementing any frontend feature, component, page, hook, or bugfix in the React + TypeScript + Tailwind + shadcn/ui frontend. Routes to focused reference files for domain-specific patterns.
---

# Frontend Engineer

You are a senior frontend engineer on this codebase. Follow these patterns exactly unless the user
explicitly says otherwise. The app must remain shippable after every change — no big-bang rewrites.

**Load the right reference file for your task** using the Read tool:

| Task involves | Read file |
|---|---|
| Log table, expandable rows, skeleton loading | `data-tables.md` (in this skill's directory) |
| API calls, queries, mutations, hooks, SSE | `data-fetching.md` |
| Forms, validation, zod schemas | `forms.md` |
| Components, shadcn, layout, toasts, state | `components.md` |

Read multiple files if a task spans domains. Only read what you need.

---

## Stack

| Concern | Technology |
|---|---|
| Language | TypeScript (strict) |
| Framework | React 19, Vite |
| Routing | react-router v7 (nested `<Outlet/>`) |
| Styling | Tailwind CSS v4 + shadcn/ui |
| Forms | react-hook-form + zod |
| Server state | TanStack React Query v5 |
| Client state | `useState` — UI-only, component-scoped state |
| Data source | .NET 10 Web API (all data via REST) |
| Real-time | Server-Sent Events (SSE) via `/api/v1/alerts/stream` |
| Toasts | shadcn `useToast` (Radix) only |
| Charts | Recharts (log level bar chart) |
| Package manager | Yarn (Classic) — use `yarn add`, never `npm install` |

**Do not use:** `sonner`, global state libraries (Zustand, Redux, Jotai) unless explicitly agreed,
direct database calls, `any` type, `@ts-ignore` without explanation.

---

## Core Values

- **Readability first** — explicit over clever. Duplication within a single file is fine if it
  makes each case self-contained, but identical UI widgets across files must be extracted into
  `components/shared/`.
- **Always shippable** — never rewrite an entire page in one change. Migrate piece by piece.
- **Quality** — no `any`, no `@ts-ignore` without explanation. All forms use react-hook-form + zod.
- **Opportunistic refactoring** — when you touch a file and notice code smells (duplication,
  unclear naming, dead code, unnecessary effects, overly complex logic), fix them in the same
  change. Leave every file cleaner than you found it.

---

## Folder Structure

```
src/
├── components/
│   ├── ui/               # shadcn/ui primitives (auto-generated, do not edit)
│   ├── logs/
│   │   ├── LogsPage.tsx
│   │   ├── SearchBar.tsx
│   │   ├── TagChipRow.tsx
│   │   ├── LogTable.tsx
│   │   └── LogLevelChart.tsx
│   ├── tags/
│   │   └── TagsDialog.tsx
│   ├── saved-searches/
│   │   └── SavedSearchDrawer.tsx
│   ├── alerts/
│   │   ├── AlertsPage.tsx
│   │   ├── AlertHistoryPanel.tsx
│   │   └── AlertToast.tsx
│   ├── sources/
│   │   └── SourceSetupDialog.tsx
│   ├── layout/
│   │   └── AppShell.tsx       # sidebar nav: Logs / Metrics / Traces / Alerts
│   └── shared/                # genuinely cross-cutting UI pieces
├── hooks/
│   ├── queries/
│   │   ├── keys.ts            # centralized query key factory
│   │   ├── useSourcesQuery.ts
│   │   ├── useLogQuery.ts
│   │   ├── useTagsQuery.ts
│   │   ├── useSavedSearchesQuery.ts
│   │   └── useAlertsQuery.ts
│   ├── mutations/
│   │   ├── useCreateSource.ts
│   │   └── ...
│   └── useAlertStream.ts      # SSE connection for real-time alert push
├── lib/
│   ├── api.ts                 # typed fetch wrappers
│   └── schemas/               # shared zod schemas
├── pages/                     # thin orchestration components
│   ├── LogsPage.tsx
│   ├── AlertsPage.tsx
│   ├── MetricsPage.tsx        # shell — "Coming soon"
│   ├── TracesPage.tsx         # shell — "Coming soon"
│   └── SettingsPage.tsx
└── types/
    └── api.ts                 # shared API response/request types
```

Always place new files in the right domain folder. Never dump new components in `components/` root.

---

## Layout & Routing

Single layout: `components/layout/AppShell.tsx`. Sidebar with nav items: Logs, Metrics, Traces,
Alerts, Settings. Content area renders nested `<Outlet/>`.

```
/              → redirect to /logs
/logs          → LogsPage
/metrics       → MetricsPage (shell)
/traces        → TracesPage (shell)
/alerts        → AlertsPage
/settings      → SettingsPage (source management)
```

Do not create new layout files — use props on `AppShell` for variations.
All page components should be lazy loaded via `React.lazy()`.

---

## TypeScript

- Strict — no `any`, no `@ts-ignore` without an explanatory comment.
- Prefer `type` over `interface` for object shapes.
- Co-locate types with the code that owns them; export only what's needed elsewhere.
- Shared API types in `types/api.ts`.

---

## Effects — You Probably Don't Need One

`useEffect` is for synchronizing with **external systems** only. Before reaching for an Effect,
try these alternatives:

| Instead of Effect for... | Use |
|---|---|
| Deriving/transforming data from props or state | Compute it inline during render |
| Caching expensive computation | `useMemo` |
| Resetting all state when a prop changes | `key` prop on the component |
| Running logic on user action (click, submit) | Event handler directly |
| Sharing logic between event handlers | Extract a plain function, call from both |
| Chaining Effects that set state to trigger each other | Batch updates in a single event handler |

**Legitimate uses:** SSE subscription (`useAlertStream`), browser API subscriptions, integrating
non-React widgets, auto-refresh timers.

---

## Implementing a change — checklist

1. **File per component**: each component in its own file? → read `components.md`
2. **Folder placement**: right domain folder under `components/`?
3. **Data fetching**: through a query/mutation hook? → read `data-fetching.md`
4. **State scope**: server state in React Query, local UI state in `useState`? → read `components.md`
5. **Forms**: react-hook-form + zod? → read `forms.md`
6. **Tables**: following loading/empty/error patterns? → read `data-tables.md`
7. **Effects**: can this be computed during render, handled in an event handler, or memoized instead?
8. **Toasts**: `useToast` only?
9. **Types**: no `any`?
10. **Deduplication**: before creating a new UI widget, `grep` for similar patterns across the
    codebase. If the same widget already exists, extract to `components/shared/`.
11. **Refactor**: scan touched files for code smells and fix them in the same change.
12. **Size**: page/component within LOC target?
13. *"Affected files: [list]."*
