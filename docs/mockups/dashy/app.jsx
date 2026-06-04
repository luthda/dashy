// ── Root app ────────────────────────────────────────────────────────
const { useState, useEffect } = React;

function App() {
  const [theme, setTheme] = useState(() => localStorage.getItem("dashy-theme") || "dark");
  const [active, setActive] = useState("logs");
  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    document.documentElement.classList.toggle("dark", theme === "dark");
    localStorage.setItem("dashy-theme", theme);
  }, [theme]);

  return (
    <div style={{ display: "flex", height: "100%", overflow: "hidden" }}>
      <Sidebar active={active} setActive={setActive} collapsed={collapsed} />
      <div style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden", minWidth: 0 }}>
        <Topbar theme={theme} toggleTheme={() => setTheme(t => t === "dark" ? "light" : "dark")} toggleSidebar={() => setCollapsed(c => !c)} />
        <main style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden", background: "var(--background)", minHeight: 0 }}>
          {active === "logs" ? <LogsPage /> : <Placeholder title={active === "metrics" ? "Metrics" : "Traces"} />}
        </main>
      </div>
    </div>
  );
}

function Placeholder({ title }) {
  return (
    <div style={{ flex: 1, display: "grid", placeItems: "center", padding: 24 }}>
      <div style={{ textAlign: "center" }}>
        <div style={{ fontSize: 15, fontWeight: 600, marginBottom: 4 }}>{title}</div>
        <div className="mono" style={{ fontSize: 12, color: "var(--muted-foreground)" }}>focusing on Logs first</div>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);
