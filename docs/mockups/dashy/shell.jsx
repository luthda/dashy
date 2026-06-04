// ── Layout shell: slim Sidebar + minimal Topbar ─────────────────────
const NAV = [
  { id: "logs", label: "Logs", icon: "logs" },
  { id: "metrics", label: "Metrics", icon: "metrics" },
  { id: "traces", label: "Traces", icon: "traces" },
];

function Sidebar({ active, setActive, collapsed }) {
  return (
    <aside style={{
      width: collapsed ? 0 : 210, flexShrink: 0, overflow: "hidden",
      background: "var(--sidebar)", borderRight: "1px solid var(--sidebar-border)",
      display: "flex", flexDirection: "column", height: "100%", transition: "width .2s ease",
    }}>
      <div style={{ height: 56, display: "flex", alignItems: "center", gap: 9, padding: "0 18px", flexShrink: 0 }}>
        <div style={{ width: 25, height: 25, borderRadius: 7, background: "var(--brand)", color: "var(--brand-fg)", display: "grid", placeItems: "center", flexShrink: 0 }}>
          <Icon name="metrics" size={15} stroke={2.6} />
        </div>
        <span style={{ fontWeight: 600, fontSize: 15, letterSpacing: "-0.02em" }}>Dashy</span>
      </div>
      <div style={{ padding: "6px 12px", flex: 1 }}>
        <nav style={{ display: "flex", flexDirection: "column", gap: 2 }}>
          {NAV.map(n => (
            <button key={n.id} onClick={() => setActive(n.id)} className="btn btn-ghost" style={{
              width: "100%", justifyContent: "flex-start", gap: 10, height: 36, fontWeight: 500, fontSize: 13.5,
              background: active === n.id ? "var(--sidebar-accent)" : "transparent",
              color: active === n.id ? "var(--foreground)" : "var(--muted-foreground)",
            }}>
              <Icon name={n.icon} size={16} stroke={active === n.id ? 2.2 : 2} />
              {n.label}
            </button>
          ))}
        </nav>
      </div>
      <div style={{ padding: 12, borderTop: "1px solid var(--sidebar-border)" }}>
        <button className="btn btn-ghost" style={{ width: "100%", justifyContent: "flex-start", gap: 10, height: 36, fontSize: 13.5, color: "var(--muted-foreground)" }}>
          <Icon name="settings" size={16} />Settings
        </button>
      </div>
    </aside>
  );
}

function Topbar({ theme, toggleTheme, toggleSidebar }) {
  return (
    <header style={{
      height: 56, flexShrink: 0, borderBottom: "1px solid var(--border)",
      display: "flex", alignItems: "center", gap: 12, padding: "0 20px", background: "var(--background)",
      position: "sticky", top: 0, zIndex: 10,
    }}>
      <button className="btn btn-ghost btn-icon" onClick={toggleSidebar} title="Toggle sidebar">
        <Icon name="sliders" size={17} />
      </button>
      <span style={{ fontWeight: 600, fontSize: 14, letterSpacing: "-0.01em" }}>Logs</span>
      <div style={{ marginLeft: "auto", display: "flex", alignItems: "center", gap: 8 }}>
        <button className="btn btn-ghost btn-icon" onClick={toggleTheme} title="Toggle theme">
          <Icon name={theme === "dark" ? "sun" : "moon"} size={17} />
        </button>
        <div style={{ width: 28, height: 28, borderRadius: 99, background: "var(--brand)", color: "var(--brand-fg)", display: "grid", placeItems: "center", fontSize: 11.5, fontWeight: 600 }}>SD</div>
      </div>
    </header>
  );
}

Object.assign(window, { Sidebar, Topbar, NAV });
