# Design: Pre-Phase 5 — Live Log Updates

_Date: 2026-06-10_
_Issue: [#12](https://github.com/luthda/dashy/issues/12)_
_Status: Implemented_

---

## Problem Statement

The Logs page already has a Live/Paused toggle wired to a polling interval, but:

1. **Live is off by default** — the user must remember to enable it each session, even though live updates are the expected default for a monitoring dashboard.
2. **The polling interval is 30 s**, which differs from the spec (60 s per the issue).
3. **The chip is visually inconsistent** — the Live/Paused button uses a standalone bordered button style, while the spec calls for the same pill-chip design as the time-range filter row.
4. **CSS tokens are missing** — `--sev-success`, `.live-dot` (pulsing animation), and `.paused-dot` are referenced in `SearchBar.tsx` but not defined in `index.css`, so the green/red coloring and the pulse animation are silently absent.

---

## Functional Spec

### States

| State | Visual | Behaviour |
|-------|--------|-----------|
| **Live** | Green chip, pulsing dot (`.live-dot`), label "Live" | Fetches logs every 60 s; interval is restarted on any state change |
| **Paused** | Red chip, static dot (`.paused-dot`), label "Paused" | No automatic fetching; manual refresh still works |

### Default state

Live is **enabled by default** when the Logs page mounts. This matches the monitoring dashboard expectation — the user never needs to opt in.

### Toggle behaviour

- Clicking the **Live** chip switches to **Paused** and immediately clears the interval.
- Clicking the **Paused** chip switches to **Live** and immediately starts a new 60-second interval.
- Only **one interval** exists at a time. The existing `useEffect` cleanup in `LogsPage` already guarantees this.
- The **Refresh** button remains functional in both states — it fires a single manual query and does not affect live state.
- Polling is also paused implicitly when `sourceId` is empty (no source selected yet). This is enforced by the existing guard in `runQuery`.
- Interval is cleaned up on component unmount.

### Position

The chip sits **immediately left of the Refresh button**, inside the SearchBar controls row. This is the current position — no layout change is needed.

### Chip design

Match the time-range picker: rendered inside the same `bg-muted` pill container with `rounded-lg p-[3px]`, single active chip using `bg-background shadow-sm rounded-md`. The chip is always "active" (it always shows the current state), so it always renders with the inset background.

Color tokens:
- Live: `text-[var(--sev-success)]`
- Paused: `text-[var(--sev-error)]`

---

## Technical Spec

This is a **frontend-only** change. No backend migration, no new API endpoint, no new component file.

### Files changed

| File | Change |
|------|--------|
| `frontend/src/index.css` | Add `--sev-success` token to `:root` and `.dark`; add `@theme inline` mapping; add `.live-dot` and `.paused-dot` CSS classes with keyframe animation |
| `frontend/src/components/logs/SearchBar.tsx` | Restyle the Live/Paused `<button>` to use the pill-chip design |
| `frontend/src/pages/LogsPage.tsx` | Change `LIVE_MS` from `30_000` → `60_000`; change initial `live` state from `false` → `true` |

### CSS tokens

```css
/* :root */
--sev-success: oklch(0.6 0.16 150);

/* .dark */
--sev-success: oklch(0.7 0.15 152);

/* @theme inline */
--color-sev-success: var(--sev-success);
```

### CSS classes

```css
.live-dot {
  width: 7px; height: 7px; border-radius: 99px;
  background: var(--sev-success);
  box-shadow: 0 0 0 0 color-mix(in oklch, var(--sev-success) 70%, transparent);
  animation: livePulse 1.8s infinite;
}

@keyframes livePulse {
  0%   { box-shadow: 0 0 0 0 color-mix(in oklch, var(--sev-success) 60%, transparent); }
  70%  { box-shadow: 0 0 0 5px transparent; }
  100% { box-shadow: 0 0 0 0 transparent; }
}

.paused-dot {
  width: 7px; height: 7px; border-radius: 99px;
  background: var(--sev-error);
}
```

### SearchBar chip markup

```tsx
<div className="bg-muted inline-flex rounded-lg p-[3px]">
  <button
    onClick={onLiveToggle}
    title={live ? "Live updates on — click to pause" : "Live updates paused — click to resume"}
    className={cn(
      "bg-background flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-medium shadow-sm",
      live ? "text-[var(--sev-success)]" : "text-[var(--sev-error)]",
    )}
  >
    <span className={live ? "live-dot" : "paused-dot"} />
    {live ? "Live" : "Paused"}
  </button>
</div>
```

### LogsPage changes

```ts
const LIVE_MS = 60_000          // was 30_000
const [live, setLive] = useState(true)  // was false
```

The polling `useEffect` and its cleanup are **unchanged** — they already handle single-interval enforcement and unmount cleanup correctly.

---

## Acceptance Criteria

Mirrors the issue's acceptance criteria exactly:

- [ ] Logs auto-refresh every 60 seconds when the Logs page is opened
- [ ] Live chip is green and displays "Live" by default
- [ ] Clicking the Live chip changes it to red "Paused"
- [ ] When paused, interval-based log refresh stops
- [ ] Clicking the Paused chip changes it back to green "Live"
- [ ] When resumed, interval-based log refresh continues every 60 seconds
- [ ] Manual refresh button still refreshes logs while live updates are active or paused
- [ ] Polling interval is cleaned up when the component unmounts or live state changes

---

## Out of Scope

Phase 5 (Alerts, SSE push, `AlertPollingService`, `useAlertStream`) is explicitly deferred. See `docs/design/dashy-core.md` for that design and `docs/plans/dashy-core.md` Phase 5 for the task list.
