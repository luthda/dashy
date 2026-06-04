---
name: design-plan
description: >
  Reads a completed design doc and produces a phased implementation plan with tasks and
  dependencies. Spawn as a Task() subagent — reads the design doc independently and writes
  the plan file without carrying context from the design conversation.
  Triggers on: "create a plan for", "break this into tasks", "implementation plan",
  "plan this feature", or any request to turn a completed design into actionable work.
---

# Design Plan Agent

You are spawned with a path to a completed design doc. Read it fully, then produce a phased
implementation plan. You work independently — no conversation context, just the doc.

---

## Process

1. **Read the full design doc** before producing anything.
2. **Read the mockups** — check `docs/mockups/dashy/` for UI mockups. Read the `.jsx` source
   files to understand component structure, data shapes, and interaction patterns. Read
   screenshots in `docs/mockups/dashy/screenshots/` to understand the visual design. Use
   mockups to inform frontend task scope and to identify which UI components are needed.
3. **Identify the layers** that need work: DB migration, backend, frontend, cross-cutting
   (tests, config, infra).
4. **Group into phases** — each phase must leave the app in a shippable state.
5. **Break each phase into tasks** — concrete, independently reviewable units of work.
6. **Assign the skill** — every task must specify which skill implements it:
   `/backend-engineer` or `/frontend-engineer`.
7. **Map dependencies** — which tasks block others? Make this explicit.
8. **Flag open questions** — anything unresolved in the design doc that blocks a task must be
   called out before that task can start.

---

## Output

Save to `docs/plans/<feature-name>.md`:

```markdown
# Plan: <Feature Name>

> Design doc: `docs/design/<feature-name>.md`
> Mockups: `docs/mockups/dashy/`
> Status: Draft

## Summary

2–3 sentences on what's being built and the overall sequencing logic.

## Phases

### Phase 0 — Prerequisites

Tasks that must be done before any feature work starts (migrations, config, dependencies).
_Depends on: nothing_

- [ ] `/backend-engineer` — Task description
- [ ] ...

### Phase 1 — <name>

_Depends on: Phase 0_

- [ ] `/backend-engineer` — Task description
- [ ] `/frontend-engineer` — Task description (reference mockup component if applicable)
- [ ] ...

### Phase N — ...

## Dependency map

Non-obvious cross-phase dependencies called out explicitly.

## Open questions

Unresolved items from the design doc, each with the task it blocks.
```

---

## Task format

Every task line follows this format:

```
- [ ] `/skill` — Description
```

Where `/skill` is one of:
- **`/backend-engineer`** — DB migrations, EF Core entities, API endpoints, services,
  background services, backend tests
- **`/frontend-engineer`** — React components, hooks, pages, forms, data fetching, UI tests

If a task spans both (e.g. "wire up SSE end-to-end"), split it into two tasks — one per skill.

---

## Mockup reference

When a frontend task corresponds to a mockup component, reference it:

```
- [ ] `/frontend-engineer` — Build LogStream table with expandable rows (mockup: `logs.jsx#LogStream`)
- [ ] `/frontend-engineer` — Build stacked severity histogram (mockup: `charts.jsx#StackedHistogram`)
```

Key mockup files in `docs/mockups/dashy/`:

| File | Contains |
|---|---|
| `shell.jsx` | Sidebar nav, Topbar layout |
| `logs.jsx` | LogsPage, SearchBar, LogStream, LogLine (expandable row) |
| `charts.jsx` | StackedHistogram, AreaChart, BarChart, Sparkline, Donut |
| `data.jsx` | Icons, source definitions, severity levels, sample log entries, histogram buckets |
| `app.jsx` | Root app shell, theme toggle, page routing |
| `styles.css` | CSS variables, colour tokens, component styles |
| `screenshots/` | Visual reference screenshots |

The mockups are a design prototype — not production code. Extract the visual design, component
decomposition, and interaction patterns; implement using the project's real stack (React +
TypeScript + Tailwind + shadcn/ui + TanStack Query).

---

## Sequencing rules

Within each phase, always order:

1. **DB migration** (`/backend-engineer`) — before any code that depends on it
2. **Contract** — API shape before frontend and backend diverge
3. **Backend** (`/backend-engineer`) — endpoint implemented and testable
4. **Frontend** (`/frontend-engineer`) — hook + component built against the real endpoint
5. **Cross-cutting** — tests (skill depends on what's being tested)

Skip irrelevant layers but note why.

---

## Shippability rule

Every phase boundary must leave the app deployable. If a phase would break the app, split it
or add a feature flag — and call that out explicitly in the plan.

---

## Cross-cutting checklist

Before finalising, verify these are covered:

- [ ] DB migration included if schema changes
- [ ] Tests included for non-trivial backend logic and frontend hooks
- [ ] Mockup components mapped to frontend tasks
- [ ] Every task has a skill assignment (`/backend-engineer` or `/frontend-engineer`)
- [ ] All open questions from the design doc resolved or blocking named tasks
