# Plan: Pre-Phase 5 — Live Log Updates

> Design doc: `docs/design/pre-phase-5-live-updates.md`
> Mockups: `docs/mockups/dashy/`
> Issue: [#12](https://github.com/luthda/dashy/issues/12) (Pre-Phase 5 scope only)
> Branch: `feat/12-live-updates`
> Status: Implemented

---

## Summary

Three targeted edits across three existing files — no new files, no new components, no backend
changes. The polling interval, toggle state, and `useEffect` cleanup already exist in
`LogsPage`; this plan fixes the four gaps between the current implementation and the spec:
missing CSS tokens/animations, incorrect poll interval (30 s vs 60 s), live-off-by-default,
and a chip style that doesn't match the time-range picker design.

---

## Phases

### Phase 1 — CSS tokens & status dot animations

_Depends on: nothing_

Unblock the visual work. The Live/Paused chip references `--sev-success`, `.live-dot`, and
`.paused-dot` in `SearchBar.tsx` but none of them exist in the frontend CSS, so the pulsing
animation and green/red coloring are silently absent. These must land first so the chip work
in Phase 2 has real tokens to reference.

- [x] `/frontend-engineer` — Add `--sev-success` CSS variable to `:root` (light) and `.dark`
  blocks in `frontend/src/index.css` (values from `docs/mockups/dashy/styles.css` lines 47
  and 92)
- [x] `/frontend-engineer` — Add `--color-sev-success: var(--sev-success)` to the
  `@theme inline` block in `frontend/src/index.css` alongside the other severity token
  mappings
- [x] `/frontend-engineer` — Append `.live-dot` class with `@keyframes livePulse` pulse
  animation and `.paused-dot` class to `frontend/src/index.css` after the `#root` block
  (animation spec: `docs/mockups/dashy/styles.css` line 242–243)

### Phase 2 — Live/Paused chip redesign

_Depends on: Phase 1 (needs `--sev-success`, `.live-dot`, `.paused-dot`)_

Restyle the existing standalone Live/Paused `<button>` in `SearchBar.tsx` to use the same
pill-chip container as the time-range picker. The chip is always in one of two states (Live or
Paused) and always shows the inset `bg-background` button, so no "active vs. inactive" variant
logic is needed — just swap color class and dot class on toggle.

- [x] `/frontend-engineer` — Replace the standalone `<button onClick={onLiveToggle}>` in
  `frontend/src/components/logs/SearchBar.tsx` with the pill-chip wrapper (`bg-muted
  rounded-lg p-[3px]` container holding an inset `bg-background shadow-sm rounded-md` button)
  matching the time-range picker (mockup: `logs.jsx` line 49–51 for Live state reference;
  chip container pattern from the range picker in the same component)
- [x] `/frontend-engineer` — Apply `text-[var(--sev-success)]` for Live state and
  `text-[var(--sev-error)]` for Paused state on the chip button
- [x] `/frontend-engineer` — Render `<span className="live-dot" />` when live and
  `<span className="paused-dot" />` when paused; add `title` attribute with descriptive tooltip

### Phase 3 — Default state & poll interval

_Depends on: nothing (independent of Phases 1–2)_

Two constant changes in `LogsPage`. No logic changes — the `useEffect` polling and its cleanup
are correct as-is.

- [x] `/frontend-engineer` — Change `LIVE_MS` from `30_000` to `60_000` in
  `frontend/src/pages/LogsPage.tsx`
- [x] `/frontend-engineer` — Change `useState(false)` to `useState(true)` for the `live` state
  in `frontend/src/pages/LogsPage.tsx` so live updates are enabled on page load

### Phase 4 — Last-update timestamp label

_Depends on: nothing (independent of Phases 1–3)_

Show the date and time of the last successful log update next to the Refresh button. The
timestamp lives in `useLogQuery` so manual searches, manual refreshes, and live poll ticks all
update it through the same code path.

- [x] `/frontend-engineer` — Add `lastUpdatedAt: Date | null` state to
  `frontend/src/hooks/useLogQuery.ts`, set in the mutation's `onSuccess`, returned from the hook
- [x] `/frontend-engineer` — Add `lastUpdatedAt` prop to `SearchBar` and render a muted
  monospace `Updated <date> <time>` label right of the Refresh button (hidden until first query)
- [x] `/frontend-engineer` — Pass `lastUpdatedAt` from `useLogQuery` through `LogsPage` to
  `SearchBar`

---

## Dependency map

```
Phase 1 (tokens)
  └─► Phase 2 (chip redesign — needs --sev-success, .live-dot, .paused-dot)

Phase 3 (interval + default)
  (independent — can be done before, during, or after Phases 1–2)
```

Phases 1 + 3 can be batched into a single commit. Phase 2 should follow so its CSS references
are already defined before the component change lands.

---

## Open questions

| # | Question | Blocks |
|---|---|---|
| 1 | ~~The mockup (`logs.jsx` line 50) renders a play `<Icon>` in Paused state and uses `muted-foreground` color; the issue spec and design doc call for a red chip with a static `paused-dot`. Which is canonical?~~ **Resolved 2026-06-10:** green pulsing dot for Live, red static dot for Paused. Mockup's play icon / muted color is superseded. | ~~Phase 2 chip redesign~~ — unblocked |

---

## Cross-cutting checklist

- [x] No DB migration needed — frontend-only change
- [x] No new API endpoint — polling calls the existing `POST /api/v1/logs/query`
- [x] No new component file — edits to three existing files only
- [x] Mockup references included for Phase 2
- [x] Every task has a skill assignment
- [x] Phase 5 (alerts, SSE, `AlertPollingService`, `useAlertStream`) is explicitly out of scope
- [x] Open question 1 resolved — green pulsing dot (Live) / red static dot (Paused)
