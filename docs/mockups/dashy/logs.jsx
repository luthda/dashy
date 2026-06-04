// ── Logs (focused, simplified) ──────────────────────────────────────
const { useState, useMemo } = React;

const RANGES = ["15m", "1h", "6h", "24h", "7d"];

function LogsPage() {
  const [query, setQuery] = useState("");
  const [range, setRange] = useState("1h");
  const [live, setLive] = useState(true);
  const [levels, setLevels] = useState(() => new Set(LEVELS.map(l => l.id)));
  const [expanded, setExpanded] = useState(null);

  const toggleLevel = (id) => setLevels(prev => {
    const n = new Set(prev); n.has(id) ? n.delete(id) : n.add(id); return n;
  });

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return LOG_ENTRIES.filter(e =>
      levels.has(e.level) &&
      (q === "" || (e.message + " " + e.service + " " + e.source + " " + e.host + " " + e.level).toLowerCase().includes(q))
    );
  }, [query, levels]);

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%", minHeight: 0, padding: "18px 22px", gap: 12 }}>
      {/* controls */}
      <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
        <label style={{
          flex: 1, minWidth: 240, display: "flex", alignItems: "center", gap: 9, height: 38, padding: "0 12px",
          border: "1px solid var(--border)", borderRadius: 9, background: "var(--card)",
        }}>
          <Icon name="search" size={16} style={{ color: "var(--muted-foreground)", flexShrink: 0 }} />
          <input value={query} onChange={e => setQuery(e.target.value)} spellCheck="false"
            placeholder="Search messages, services, hosts…"
            style={{ border: "none", background: "transparent", outline: "none", color: "var(--foreground)", fontSize: 13.5, width: "100%", fontFamily: "inherit" }} />
          {query && <button onClick={() => setQuery("")} className="btn btn-ghost btn-icon" style={{ width: 24, height: 24 }}><Icon name="x" size={13} /></button>}
        </label>
        <div style={{ display: "inline-flex", gap: 2, padding: 3, background: "var(--muted)", borderRadius: 8 }}>
          {RANGES.map(r => (
            <button key={r} onClick={() => setRange(r)} className="mono" style={{
              border: "none", cursor: "pointer", padding: "6px 10px", fontSize: 12, fontWeight: 500, borderRadius: 6,
              background: range === r ? "var(--background)" : "transparent",
              color: range === r ? "var(--foreground)" : "var(--muted-foreground)",
              boxShadow: range === r ? "0 1px 2px rgb(0 0 0 / 0.08)" : "none",
            }}>{r}</button>
          ))}
        </div>
        <button onClick={() => setLive(v => !v)} className="btn btn-outline" style={{ height: 38, fontSize: 12.5, color: live ? "var(--sev-success)" : "var(--muted-foreground)", borderColor: live ? "color-mix(in oklch, var(--sev-success) 35%, transparent)" : "var(--border)" }}>
          {live ? <span className="live-dot"></span> : <Icon name="play" size={13} />}
          {live ? "Live" : "Paused"}
        </button>
        <button className="btn btn-ghost btn-icon" title="Refresh" style={{ height: 38, width: 38 }}><Icon name="refresh" size={16} /></button>
      </div>

      {/* histogram + level filter */}
      <div className="card" style={{ padding: "11px 14px 8px" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 6, flexWrap: "wrap", gap: 8 }}>
          <span style={{ fontSize: 12.5, color: "var(--muted-foreground)" }}>
            <span className="mono" style={{ color: "var(--foreground)", fontWeight: 600 }}>{filtered.length.toLocaleString()}</span> events shown · last {range}
          </span>
          <div style={{ display: "flex", gap: 5, flexWrap: "wrap" }}>
            {LEVELS.map(l => {
              const on = levels.has(l.id);
              return (
                <button key={l.id} onClick={() => toggleLevel(l.id)} style={{
                  display: "flex", alignItems: "center", gap: 6, height: 24, padding: "0 9px", borderRadius: 99,
                  border: "1px solid var(--border)", cursor: "pointer", fontFamily: "inherit",
                  background: on ? "var(--card)" : "transparent", opacity: on ? 1 : 0.45,
                  fontSize: 11.5, color: "var(--foreground)", fontWeight: 500,
                }}>
                  <span style={{ width: 8, height: 8, borderRadius: 2, background: l.color }}></span>{l.label}
                </button>
              );
            })}
          </div>
        </div>
        <StackedHistogram data={BUCKETS} levels={LEVELS} activeLevels={levels} height={78} />
      </div>

      {/* stream */}
      <LogStream rows={filtered} expanded={expanded} setExpanded={setExpanded} live={live} />
    </div>
  );
}

// ── log stream ──────────────────────────────────────────────────────
function LogStream({ rows, expanded, setExpanded, live }) {
  return (
    <div className="card" style={{ flex: 1, minHeight: 0, display: "flex", flexDirection: "column", overflow: "hidden" }}>
      <div style={{ display: "grid", gridTemplateColumns: "18px 132px 72px 144px 1fr", gap: 12, padding: "9px 14px", borderBottom: "1px solid var(--border)", fontSize: 11, fontWeight: 500, color: "var(--muted-foreground)", textTransform: "uppercase", letterSpacing: "0.04em", flexShrink: 0 }}>
        <span></span><span>Time</span><span>Level</span><span>Service</span>
        <span style={{ display: "flex", justifyContent: "space-between", textTransform: "none", letterSpacing: 0 }}>
          <span style={{ textTransform: "uppercase", letterSpacing: "0.04em" }}>Message</span>
          {live && <span style={{ display: "flex", alignItems: "center", gap: 6 }}><span className="live-dot"></span><span style={{ color: "var(--sev-success)" }}>streaming</span></span>}
        </span>
      </div>
      <div style={{ flex: 1, overflowY: "auto", minHeight: 0 }}>
        {rows.length === 0 ? (
          <div style={{ padding: "48px 0", textAlign: "center", color: "var(--muted-foreground)", fontSize: 13 }}>
            No logs match your search.
          </div>
        ) : rows.map(e => (
          <LogLine key={e.id} e={e} open={expanded === e.id} onToggle={() => setExpanded(expanded === e.id ? null : e.id)} />
        ))}
      </div>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "8px 14px", borderTop: "1px solid var(--border)", fontSize: 11.5, color: "var(--muted-foreground)", flexShrink: 0 }}>
        <span className="mono">{rows.length} lines</span>
        <span>Click a line to expand · scroll to load older</span>
      </div>
    </div>
  );
}

function LogLine({ e, open, onToggle }) {
  const lv = LEVEL_MAP[e.level];
  const src = SOURCES.find(s => s.id === e.source);
  return (
    <>
      <div className={`log-row ${open ? "open expanded" : ""}`} onClick={onToggle} style={{ gridTemplateColumns: "18px 132px 72px 144px 1fr" }}>
        <span className="chev" style={{ alignSelf: "center" }}><Icon name="chevR" size={12} /></span>
        <span className="ts">{e.t}</span>
        <span><span className={`sev-badge ${lv.cls}`}>{lv.label}</span></span>
        <span className="svc">{e.service}</span>
        <span className="msg">{e.message}</span>
      </div>
      {open && (
        <div className="log-detail">
          <div style={{ display: "flex", gap: 8, marginBottom: 10, flexWrap: "wrap", alignItems: "center" }}>
            <span style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 11.5, color: "var(--muted-foreground)" }}>
              <span style={{ width: 14, height: 14, borderRadius: 3, background: src.color, color: "#fff", display: "grid", placeItems: "center", fontSize: 7.5, fontWeight: 700 }}>{src.abbr}</span>
              {src.name}
            </span>
            <span style={{ width: 1, height: 14, background: "var(--border)" }}></span>
            <button className="btn btn-outline" style={{ height: 26, fontSize: 11.5 }}><Icon name="copy" size={12} />Copy</button>
            {e.traceId && <button className="btn btn-outline" style={{ height: 26, fontSize: 11.5, color: "var(--brand)" }}><Icon name="traces" size={12} />Trace {e.traceId}</button>}
          </div>
          <div className="kv-grid">
            <span className="key">message</span><span className="val" style={{ color: lv.color }}>{e.message}</span>
            <span className="key">host</span><span className="val">{e.host}</span>
            {Object.entries(e.fields).map(([k, v]) => (
              <React.Fragment key={k}>
                <span className="key">{k}</span>
                <span className="val">{typeof v === "boolean" ? String(v) : v}</span>
              </React.Fragment>
            ))}
          </div>
        </div>
      )}
    </>
  );
}

Object.assign(window, { LogsPage });
