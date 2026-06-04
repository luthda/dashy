// ── Chart components (clean SVG, shadcn-styled) ─────────────────────
const { useState, useRef, useEffect, useMemo } = React;

function useTooltip() {
  const [tip, setTip] = useState(null);
  return [tip, setTip];
}

// Smooth area/line chart with hover crosshair
function AreaChart({ data, height = 260, color = "var(--chart-1)", id = "a" }) {
  const wrapRef = useRef(null);
  const [w, setW] = useState(600);
  const [hover, setHover] = useState(null);
  useEffect(() => {
    const ro = new ResizeObserver(es => setW(es[0].contentRect.width));
    if (wrapRef.current) ro.observe(wrapRef.current);
    return () => ro.disconnect();
  }, []);

  const pad = { t: 12, r: 8, b: 26, l: 38 };
  const iw = Math.max(10, w - pad.l - pad.r);
  const ih = height - pad.t - pad.b;
  const max = Math.max(...data.map(d => d.v)) * 1.12;
  const min = 0;
  const x = i => pad.l + (i / (data.length - 1)) * iw;
  const y = v => pad.t + ih - ((v - min) / (max - min)) * ih;

  const pts = data.map((d, i) => [x(i), y(d.v)]);
  // Catmull-Rom -> bezier for smooth line
  const path = useMemo(() => {
    if (pts.length < 2) return "";
    let d = `M ${pts[0][0]},${pts[0][1]}`;
    for (let i = 0; i < pts.length - 1; i++) {
      const p0 = pts[i - 1] || pts[i];
      const p1 = pts[i], p2 = pts[i + 1], p3 = pts[i + 2] || p2;
      const c1x = p1[0] + (p2[0] - p0[0]) / 6, c1y = p1[1] + (p2[1] - p0[1]) / 6;
      const c2x = p2[0] - (p3[0] - p1[0]) / 6, c2y = p2[1] - (p3[1] - p1[1]) / 6;
      d += ` C ${c1x},${c1y} ${c2x},${c2y} ${p2[0]},${p2[1]}`;
    }
    return d;
  }, [w, data]);
  const areaPath = path + ` L ${x(data.length - 1)},${pad.t + ih} L ${x(0)},${pad.t + ih} Z`;

  const ticks = 4;
  const gridY = Array.from({ length: ticks + 1 }, (_, i) => pad.t + (ih / ticks) * i);
  const yVals = Array.from({ length: ticks + 1 }, (_, i) => max - (max / ticks) * i);

  function onMove(e) {
    const rect = wrapRef.current.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    let idx = Math.round(((mx - pad.l) / iw) * (data.length - 1));
    idx = Math.max(0, Math.min(data.length - 1, idx));
    setHover(idx);
  }

  return (
    <div ref={wrapRef} style={{ position: "relative", width: "100%" }} onMouseMove={onMove} onMouseLeave={() => setHover(null)}>
      <svg width={w} height={height} style={{ display: "block", overflow: "visible" }}>
        <defs>
          <linearGradient id={`grad-${id}`} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={color} stopOpacity="0.25" />
            <stop offset="100%" stopColor={color} stopOpacity="0" />
          </linearGradient>
        </defs>
        {gridY.map((gy, i) => (
          <g key={i}>
            <line x1={pad.l} y1={gy} x2={w - pad.r} y2={gy} stroke="var(--border)" strokeWidth="1" strokeDasharray={i === ticks ? "0" : "3 3"} opacity={i === ticks ? 1 : 0.6} />
            <text x={pad.l - 8} y={gy + 3} textAnchor="end" fontSize="10" fill="var(--muted-foreground)" className="mono">
              {fmtCompact(yVals[i])}
            </text>
          </g>
        ))}
        <path d={areaPath} fill={`url(#grad-${id})`} />
        <path d={path} fill="none" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        {data.map((d, i) => (i % Math.ceil(data.length / 7) === 0 || i === data.length - 1) && (
          <text key={i} x={x(i)} y={height - 8} textAnchor="middle" fontSize="10" fill="var(--muted-foreground)" className="mono">{d.l}</text>
        ))}
        {hover != null && (
          <g>
            <line x1={x(hover)} y1={pad.t} x2={x(hover)} y2={pad.t + ih} stroke="var(--muted-foreground)" strokeWidth="1" strokeDasharray="3 3" />
            <circle cx={x(hover)} cy={y(data[hover].v)} r="4.5" fill="var(--background)" stroke={color} strokeWidth="2" />
          </g>
        )}
      </svg>
      {hover != null && (
        <div style={{
          position: "absolute", left: Math.min(Math.max(x(hover), 60), w - 60), top: y(data[hover].v) - 14,
          transform: "translate(-50%, -100%)", pointerEvents: "none",
          background: "var(--popover)", color: "var(--popover-foreground)",
          border: "1px solid var(--border)", borderRadius: "8px", padding: "6px 10px",
          fontSize: "12px", boxShadow: "0 4px 16px rgb(0 0 0 / 0.12)", whiteSpace: "nowrap", zIndex: 5
        }}>
          <div style={{ color: "var(--muted-foreground)", fontSize: "11px", marginBottom: "2px" }}>{data[hover].full || data[hover].l}</div>
          <div style={{ fontWeight: 600 }} className="mono">{fmtMoney(data[hover].v)}</div>
        </div>
      )}
    </div>
  );
}

// Grouped/single bar chart
function BarChart({ data, height = 220, color = "var(--chart-1)" }) {
  const wrapRef = useRef(null);
  const [w, setW] = useState(500);
  const [hover, setHover] = useState(null);
  useEffect(() => {
    const ro = new ResizeObserver(es => setW(es[0].contentRect.width));
    if (wrapRef.current) ro.observe(wrapRef.current);
    return () => ro.disconnect();
  }, []);
  const pad = { t: 10, r: 8, b: 28, l: 34 };
  const iw = Math.max(10, w - pad.l - pad.r);
  const ih = height - pad.t - pad.b;
  const max = Math.max(...data.map(d => d.v)) * 1.15;
  const bw = (iw / data.length) * 0.62;
  const gap = (iw / data.length);

  const ticks = 3;
  const gridY = Array.from({ length: ticks + 1 }, (_, i) => pad.t + (ih / ticks) * i);
  const yVals = Array.from({ length: ticks + 1 }, (_, i) => max - (max / ticks) * i);

  return (
    <div ref={wrapRef} style={{ position: "relative", width: "100%" }}>
      <svg width={w} height={height} style={{ display: "block" }}>
        {gridY.map((gy, i) => (
          <g key={i}>
            <line x1={pad.l} y1={gy} x2={w - pad.r} y2={gy} stroke="var(--border)" strokeWidth="1" strokeDasharray={i === ticks ? "0" : "3 3"} opacity={i === ticks ? 1 : 0.6} />
            <text x={pad.l - 8} y={gy + 3} textAnchor="end" fontSize="10" fill="var(--muted-foreground)" className="mono">{fmtCompact(yVals[i])}</text>
          </g>
        ))}
        {data.map((d, i) => {
          const bh = ((d.v) / max) * ih;
          const bx = pad.l + gap * i + (gap - bw) / 2;
          const by = pad.t + ih - bh;
          return (
            <g key={i} onMouseEnter={() => setHover(i)} onMouseLeave={() => setHover(null)} style={{ cursor: "pointer" }}>
              <rect x={pad.l + gap * i} y={pad.t} width={gap} height={ih} fill="transparent" />
              <rect x={bx} y={by} width={bw} height={bh} rx="4" fill={color} opacity={hover == null || hover === i ? 1 : 0.4} style={{ transition: "opacity .15s" }} />
              <text x={bx + bw / 2} y={height - 9} textAnchor="middle" fontSize="10" fill="var(--muted-foreground)">{d.l}</text>
            </g>
          );
        })}
      </svg>
      {hover != null && (
        <div style={{
          position: "absolute", left: pad.l + gap * hover + gap / 2, top: pad.t + ih - (data[hover].v / max) * ih - 10,
          transform: "translate(-50%, -100%)", pointerEvents: "none",
          background: "var(--popover)", border: "1px solid var(--border)", borderRadius: "8px", padding: "5px 9px",
          fontSize: "12px", boxShadow: "0 4px 16px rgb(0 0 0 / 0.12)", whiteSpace: "nowrap", zIndex: 5
        }}>
          <span style={{ color: "var(--muted-foreground)" }}>{data[hover].full || data[hover].l}: </span>
          <span style={{ fontWeight: 600 }} className="mono">{fmtCompact(data[hover].v)}</span>
        </div>
      )}
    </div>
  );
}

// Tiny sparkline for KPI cards
function Sparkline({ data, color = "var(--chart-1)", w = 96, h = 32 }) {
  const max = Math.max(...data), min = Math.min(...data);
  const rng = max - min || 1;
  const pts = data.map((v, i) => [(i / (data.length - 1)) * w, h - ((v - min) / rng) * (h - 4) - 2]);
  let d = `M ${pts[0][0]},${pts[0][1]}`;
  for (let i = 1; i < pts.length; i++) {
    const px = (pts[i - 1][0] + pts[i][0]) / 2;
    d += ` Q ${pts[i - 1][0]},${pts[i - 1][1]} ${px},${(pts[i - 1][1] + pts[i][1]) / 2}`;
    d += ` T ${pts[i][0]},${pts[i][1]}`;
  }
  return (
    <svg width={w} height={h} style={{ display: "block", overflow: "visible" }}>
      <path d={d} fill="none" stroke={color} strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

// Donut breakdown
function Donut({ data, size = 150 }) {
  const total = data.reduce((s, d) => s + d.v, 0);
  const r = size / 2 - 12, cx = size / 2, cy = size / 2;
  const circ = 2 * Math.PI * r;
  let acc = 0;
  const [hover, setHover] = useState(null);
  return (
    <div style={{ position: "relative", width: size, height: size }}>
      <svg width={size} height={size} style={{ transform: "rotate(-90deg)" }}>
        {data.map((d, i) => {
          const frac = d.v / total;
          const dash = frac * circ;
          const seg = (
            <circle key={i} cx={cx} cy={cy} r={r} fill="none" stroke={d.color} strokeWidth={hover === i ? 22 : 18}
              strokeDasharray={`${dash} ${circ - dash}`} strokeDashoffset={-acc * circ}
              onMouseEnter={() => setHover(i)} onMouseLeave={() => setHover(null)}
              style={{ transition: "stroke-width .15s", cursor: "pointer" }} />
          );
          acc += frac;
          return seg;
        })}
      </svg>
      <div style={{ position: "absolute", inset: 0, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", pointerEvents: "none" }}>
        <div style={{ fontSize: "11px", color: "var(--muted-foreground)" }}>{hover != null ? data[hover].l : "Total"}</div>
        <div style={{ fontSize: "18px", fontWeight: 600 }} className="mono">{hover != null ? Math.round(data[hover].v / total * 100) + "%" : fmtCompact(total)}</div>
      </div>
    </div>
  );
}

// ── formatters ──────────────────────────────────────────────────────
function fmtMoney(v) { return "$" + v.toLocaleString("en-US", { maximumFractionDigits: 0 }); }
function fmtCompact(v) {
  if (v >= 1e6) return "$" + (v / 1e6).toFixed(1).replace(/\.0$/, "") + "M";
  if (v >= 1e3) return "$" + (v / 1e3).toFixed(1).replace(/\.0$/, "") + "K";
  return "$" + Math.round(v);
}

// ── Stacked severity histogram (log volume over time) ───────────────
function StackedHistogram({ data, levels, height = 132, activeLevels }) {
  const wrapRef = useRef(null);
  const [w, setW] = useState(760);
  const [hover, setHover] = useState(null);
  useEffect(() => {
    const ro = new ResizeObserver(es => setW(es[0].contentRect.width));
    if (wrapRef.current) ro.observe(wrapRef.current);
    return () => ro.disconnect();
  }, []);
  const order = ["info", "debug", "trace", "warn", "error"];
  const lv = id => levels.find(l => l.id === id);
  const isOn = id => !activeLevels || activeLevels.has(id);

  const pad = { t: 8, r: 0, b: 4, l: 0 };
  const iw = Math.max(10, w - pad.l - pad.r);
  const ih = height - pad.t - pad.b;
  const totals = data.map(d => order.reduce((s, k) => s + (isOn(k) ? d[k] : 0), 0));
  const max = Math.max(...totals, 1) * 1.08;
  const n = data.length;
  const slot = iw / n;
  const bw = Math.max(2, slot * 0.74);

  return (
    <div ref={wrapRef} style={{ position: "relative", width: "100%" }} onMouseLeave={() => setHover(null)}>
      <svg width={w} height={height} style={{ display: "block" }}
        onMouseMove={e => {
          const rect = wrapRef.current.getBoundingClientRect();
          let idx = Math.floor((e.clientX - rect.left - pad.l) / slot);
          setHover(Math.max(0, Math.min(n - 1, idx)));
        }}>
        {data.map((d, i) => {
          let yAcc = pad.t + ih;
          const x = pad.l + i * slot + (slot - bw) / 2;
          const dim = hover != null && hover !== i;
          return (
            <g key={i} opacity={dim ? 0.45 : 1} style={{ transition: "opacity .1s" }}>
              {order.map(k => {
                if (!isOn(k)) return null;
                const h = (d[k] / max) * ih;
                if (h <= 0) return null;
                yAcc -= h;
                return <rect key={k} x={x} y={yAcc} width={bw} height={h} fill={lv(k).color} rx={bw > 4 ? 1 : 0} />;
              })}
            </g>
          );
        })}
        <line x1={pad.l} y1={pad.t + ih + 0.5} x2={w - pad.r} y2={pad.t + ih + 0.5} stroke="var(--border)" strokeWidth="1" />
      </svg>
      {hover != null && (
        <div style={{
          position: "absolute", left: Math.min(Math.max(pad.l + hover * slot + slot / 2, 90), w - 90),
          top: 0, transform: "translate(-50%, -102%)", pointerEvents: "none", zIndex: 6,
          background: "var(--popover)", border: "1px solid var(--border)", borderRadius: 8,
          padding: "8px 10px", boxShadow: "0 6px 20px rgb(0 0 0 / 0.14)", whiteSpace: "nowrap",
        }}>
          <div style={{ fontSize: 11, color: "var(--muted-foreground)", marginBottom: 5 }} className="mono">
            bucket {hover + 1} · {totals[hover]} events
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "auto auto", gap: "3px 12px" }}>
            {["error", "warn", "info", "debug", "trace"].map(k => isOn(k) && (
              <div key={k} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 11.5 }}>
                <span style={{ width: 8, height: 8, borderRadius: 2, background: lv(k).color }}></span>
                <span style={{ color: "var(--muted-foreground)", textTransform: "capitalize" }}>{k}</span>
                <span className="mono" style={{ marginLeft: "auto", fontWeight: 600 }}>{data[hover][k]}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

Object.assign(window, { AreaChart, BarChart, Sparkline, Donut, StackedHistogram, fmtMoney, fmtCompact });
