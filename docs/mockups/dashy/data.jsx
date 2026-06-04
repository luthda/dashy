// ── Icons (Lucide-style, 24x24 stroke) ──────────────────────────────
function Icon({ name, size = 18, stroke = 2, style }) {
  const p = ICONS[name] || "";
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor"
      strokeWidth={stroke} strokeLinecap="round" strokeLinejoin="round" style={style} aria-hidden="true">
      {p}
    </svg>
  );
}
const ICONS = {
  logs: <><line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><line x1="3" y1="6" x2="3.01" y2="6"/><line x1="3" y1="12" x2="3.01" y2="12"/><line x1="3" y1="18" x2="3.01" y2="18"/></>,
  metrics: <path d="M3 12h4l3 8 4-16 3 8h4"/>,
  traces: <><circle cx="6" cy="6" r="2.4"/><circle cx="6" cy="18" r="2.4"/><circle cx="18" cy="12" r="2.4"/><path d="M8.4 6H14a2 2 0 0 1 2 2v2.5M8.4 18H14a2 2 0 0 0 2-2v-2.5"/></>,
  dashboards: <><rect x="3" y="3" width="7" height="9" rx="1"/><rect x="14" y="3" width="7" height="5" rx="1"/><rect x="14" y="12" width="7" height="9" rx="1"/><rect x="3" y="16" width="7" height="5" rx="1"/></>,
  alerts: <><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></>,
  sources: <><ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/><path d="M3 12c0 1.66 4 3 9 3s9-1.34 9-3"/></>,
  pipelines: <><rect x="3" y="3" width="6" height="6" rx="1"/><rect x="15" y="15" width="6" height="6" rx="1"/><path d="M9 6h6a2 2 0 0 1 2 2v7"/></>,
  settings: <><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></>,
  bell: <><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></>,
  search: <><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></>,
  sun: <><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41"/></>,
  moon: <path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9z"/>,
  chevDown: <polyline points="6 9 12 15 18 9"/>,
  chevR: <polyline points="9 18 15 12 9 6"/>,
  arrowUp: <><line x1="12" y1="19" x2="12" y2="5"/><polyline points="5 12 12 5 19 12"/></>,
  arrowDown: <><line x1="12" y1="5" x2="12" y2="19"/><polyline points="19 12 12 19 5 12"/></>,
  download: <><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></>,
  plus: <><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></>,
  more: <><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/><circle cx="5" cy="12" r="1"/></>,
  x: <><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></>,
  refresh: <><polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/></>,
  clock: <><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></>,
  play: <polygon points="5 3 19 12 5 21 5 3"/>,
  pause: <><rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/></>,
  sliders: <><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><line x1="1" y1="14" x2="7" y2="14"/><line x1="9" y1="8" x2="15" y2="8"/><line x1="17" y1="16" x2="23" y2="16"/></>,
  copy: <><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></>,
  external: <><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/></>,
  server: <><rect x="2" y="2" width="20" height="8" rx="2"/><rect x="2" y="14" width="20" height="8" rx="2"/><line x1="6" y1="6" x2="6.01" y2="6"/><line x1="6" y1="18" x2="6.01" y2="18"/></>,
  help: <><circle cx="12" cy="12" r="10"/><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"/><line x1="12" y1="17" x2="12.01" y2="17"/></>,
  check: <polyline points="20 6 9 17 4 12"/>,
  bookmark: <path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/>,
  filter: <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3"/>,
  share: <><circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><line x1="8.59" y1="13.51" x2="15.42" y2="17.49"/><line x1="15.41" y1="6.51" x2="8.59" y2="10.49"/></>,
  zap: <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/>,
};

// ── Sources (configurable cloud log sources) ────────────────────────
const SOURCES = [
  { id: "azure",      name: "Azure App Insights", kind: "Application Insights", abbr: "Az", status: "healthy",  rate: "4.2k/s", color: "var(--sev-info)" },
  { id: "gcp",        name: "GCP Operations",     kind: "Cloud Logging",        abbr: "GC", status: "healthy",  rate: "2.8k/s", color: "var(--sev-success)" },
  { id: "loki",       name: "Grafana Loki",       kind: "Loki",                 abbr: "Lo", status: "healthy",  rate: "6.1k/s", color: "var(--sev-warn)" },
  { id: "cloudwatch", name: "AWS CloudWatch",     kind: "CloudWatch Logs",      abbr: "CW", status: "degraded", rate: "1.4k/s", color: "var(--sev-error)" },
  { id: "promtail",   name: "Promtail",           kind: "Agent",                abbr: "Pt", status: "healthy",  rate: "6.1k/s", color: "var(--sev-trace)" },
];

// ── Severity levels ─────────────────────────────────────────────────
const LEVELS = [
  { id: "error", label: "Error", cls: "sev-error", bg: "bg-error", color: "var(--sev-error)" },
  { id: "warn",  label: "Warn",  cls: "sev-warn",  bg: "bg-warn",  color: "var(--sev-warn)" },
  { id: "info",  label: "Info",  cls: "sev-info",  bg: "bg-info",  color: "var(--sev-info)" },
  { id: "debug", label: "Debug", cls: "sev-debug", bg: "bg-debug", color: "var(--sev-debug)" },
  { id: "trace", label: "Trace", cls: "sev-trace", bg: "bg-trace", color: "var(--sev-trace)" },
];
const LEVEL_MAP = Object.fromEntries(LEVELS.map(l => [l.id, l]));

// ── Log entries ─────────────────────────────────────────────────────
function ts(h, m, s, ms) { return `09:${String(m).padStart(2,"0")}:${String(s).padStart(2,"0")}.${String(ms).padStart(3,"0")}`; }
let _lid = 1000;
const E = (level, source, service, host, message, fields, traceId) =>
  ({ id: ++_lid, level, source, service, host, message, fields: fields || {}, traceId });

const LOG_ENTRIES = [
  E("error","cloudwatch","checkout-api","ip-10-0-3-21","POST /v2/checkout 500 — upstream payments timeout after 30000ms",{status:503,duration_ms:30004,method:"POST",path:"/v2/checkout",order_id:"ord_91x2",region:"us-east-1",pod:"checkout-api-7f9c-2xk"},"4af9c1e7"),
  E("warn","azure","checkout-api","aks-pool-2-9","p99 latency 1240ms exceeds SLO threshold 800ms",{p99_ms:1240,slo_ms:800,window:"5m",region:"eastus"}),
  E("info","loki","auth-service","gke-prod-a-7f","user authenticated user_id=u_4821 method=oauth_google",{user_id:"u_4821",method:"oauth_google",ip:"203.0.113.44",duration_ms:88}),
  E("error","azure","payments","aks-pool-1-3","stripe.ChargeError: card_declined (insufficient_funds)",{provider:"stripe",code:"card_declined",decline:"insufficient_funds",customer:"cus_Ab12",amount:4899},"4af9c1e7"),
  E("debug","loki","gateway","gke-prod-b-1c","routing GET /healthz → svc:web-frontend (cache hit)",{route:"/healthz",target:"web-frontend",cache:"hit"}),
  E("info","gcp","ingest-worker","gke-prod-a-7f","processed batch ingest_8843 records=5120 in 812ms",{batch:"ingest_8843",records:5120,duration_ms:812}),
  E("error","loki","auth-service","gke-prod-a-7f","panic: runtime error: invalid memory address or nil pointer dereference",{goroutine:48211,signal:"SIGSEGV",pod:"auth-service-5b8-9qd"},"7c20ba01"),
  E("warn","gcp","ingest-worker","gke-prod-c-4a","retry 3/5 for batch ingest_8842 after 503 from sink",{batch:"ingest_8842",attempt:3,max:5,sink:"bigquery"}),
  E("info","cloudwatch","checkout-api","ip-10-0-3-21","POST /v2/checkout 200 in 142ms order_id=ord_91x4",{status:200,duration_ms:142,order_id:"ord_91x4",method:"POST"}),
  E("error","cloudwatch","postgres","ip-10-0-9-12","FATAL: remaining connection slots are reserved for non-replication superuser connections",{db:"orders",max_conn:100,active:100,region:"us-east-1"}),
  E("trace","loki","gateway","gke-prod-b-1c","span start GET /v2/checkout trace=4af9c1e7 parent=root",{trace_id:"4af9c1e7",span_id:"a1",op:"http.server"}),
  E("info","azure","payments","aks-pool-1-3","charge succeeded amount=4899 currency=usd customer=cus_Cd34",{amount:4899,currency:"usd",customer:"cus_Cd34",provider:"stripe"}),
  E("warn","loki","gateway","gke-prod-b-1c","rate limit approaching: 9210/10000 req/min for tenant acme",{tenant:"acme",current:9210,limit:10000,window:"1m"}),
  E("debug","azure","checkout-api","aks-pool-2-9","feature flag 'new_cart_ui' = true for user_id=u_4821",{flag:"new_cart_ui",value:true,user_id:"u_4821"}),
  E("info","gcp","notification-svc","gke-prod-c-4a","sent 312 push notifications campaign=reactivation",{count:312,campaign:"reactivation",channel:"push"}),
  E("error","loki","redis","gke-prod-a-7f","READONLY You can't write against a read only replica.",{cmd:"SET",key:"session:u_4821",replica:"redis-2",region:"us-central1"}),
  E("warn","azure","auth-service","aks-pool-2-9","JWT signing key rotates in 2h; refresh recommended",{kid:"k_2026_06",rotate_in:"2h"}),
  E("debug","cloudwatch","postgres","ip-10-0-9-12","SELECT * FROM orders WHERE status=$1 — 14ms 38 rows",{duration_ms:14,rows:38,table:"orders"}),
  E("trace","azure","payments","aks-pool-1-3","span charge.authorize 88ms trace=4af9c1e7",{trace_id:"4af9c1e7",span_id:"b3",duration_ms:88,op:"charge.authorize"}),
  E("info","loki","web-frontend","gke-prod-b-1c","GET /api/v1/cart 200 in 36ms user_id=u_4821",{status:200,duration_ms:36,path:"/api/v1/cart"}),
  E("error","gcp","gateway","gke-prod-b-1c","upstream connect error or disconnect/reset before headers. reset reason: connection failure",{upstream:"payments:8080",reset:"connection failure",status:503},"9f01dd2a"),
  E("warn","loki","web-frontend","gke-prod-b-1c","deprecated endpoint /api/v1/profile called by client 1.8.2",{path:"/api/v1/profile",client:"ios/1.8.2",sunset:"2026-08-01"}),
  E("debug","loki","redis","gke-prod-a-7f","GET session:u_4821 → HIT ttl=540s",{cmd:"GET",key:"session:u_4821",result:"HIT",ttl:540}),
  E("info","cloudwatch","ingest-worker","ip-10-0-3-21","processed batch ingest_8844 records=4980 in 760ms",{batch:"ingest_8844",records:4980,duration_ms:760}),
  E("error","azure","checkout-api","aks-pool-2-9","unhandled rejection: PaymentTimeout at processOrder (order.js:214)",{file:"order.js",line:214,order_id:"ord_91x9"},"4af9c1e7"),
  E("info","gcp","auth-service","gke-prod-a-7f","user logged out user_id=u_3310 session=8h12m",{user_id:"u_3310",session:"8h12m"}),
  E("trace","gcp","checkout-api","aks-pool-2-9","span db.query orders 14ms trace=4af9c1e7",{trace_id:"4af9c1e7",span_id:"c7",duration_ms:14,op:"db.query"}),
  E("warn","cloudwatch","postgres","ip-10-0-9-12","autovacuum running long on table events (1280s)",{table:"events",duration_s:1280}),
  E("info","loki","gateway","gke-prod-b-1c","GET /v2/products 200 in 58ms cache=miss",{status:200,duration_ms:58,cache:"miss"}),
  E("debug","azure","notification-svc","aks-pool-1-3","evaluating template welcome_v3 for 312 recipients",{template:"welcome_v3",recipients:312}),
  E("info","gcp","payments","aks-pool-1-3","refund issued amount=1299 currency=usd customer=cus_Ef56",{amount:1299,currency:"usd",customer:"cus_Ef56",type:"refund"}),
  E("error","cloudwatch","gateway","ip-10-0-3-21","TLS handshake failed: certificate has expired for api.internal",{host:"api.internal",error:"certificate expired",expiry:"2026-06-03"}),
];

// assign descending timestamps (newest first) starting 09:14:32
(function () {
  const r = seedRand(7);
  let sec = 14 * 60 + 32, ms = 540;
  for (const e of LOG_ENTRIES) {
    e.t = `09:${String(Math.floor(sec / 60) % 60).padStart(2, "0")}:${String(sec % 60).padStart(2, "0")}.${String(ms).padStart(3, "0")}`;
    let drop = Math.floor(r() * 2400) + 80;
    ms -= drop;
    while (ms < 0) { ms += 1000; sec -= 1; }
  }
})();

// ── Histogram buckets (per-severity volume over time) ───────────────
function seedRand(seed) { let s = seed; return () => { s = (s * 1103515245 + 12345) & 0x7fffffff; return s / 0x7fffffff; }; }
function buildBuckets(n) {
  const r = seedRand(42);
  const out = [];
  for (let i = 0; i < n; i++) {
    const spike = i > n * 0.62 && i < n * 0.72;
    out.push({
      i,
      info:  Math.round(58 + r() * 40 + Math.sin(i / 5) * 12),
      debug: Math.round(26 + r() * 22),
      warn:  Math.round(6 + r() * 9 + (spike ? r() * 14 : 0)),
      error: Math.round(1 + r() * 3 + (spike ? 8 + r() * 22 : 0)),
      trace: Math.round(10 + r() * 14),
    });
  }
  return out;
}
const BUCKETS = buildBuckets(60);

// ── Facet counts (derived-ish, fixed for realism) ───────────────────
const FACET_SERVICES = [
  { id: "checkout-api", count: 18420 }, { id: "gateway", count: 14210 }, { id: "auth-service", count: 9870 },
  { id: "payments", count: 7640 }, { id: "ingest-worker", count: 6120 }, { id: "web-frontend", count: 5430 },
  { id: "postgres", count: 3210 }, { id: "redis", count: 2890 }, { id: "notification-svc", count: 1240 },
];
const FACET_HOSTS = [
  { id: "gke-prod-a-7f", count: 21400 }, { id: "gke-prod-b-1c", count: 18900 }, { id: "aks-pool-2-9", count: 12300 },
  { id: "ip-10-0-3-21", count: 8700 }, { id: "aks-pool-1-3", count: 6400 }, { id: "ip-10-0-9-12", count: 2100 },
];
const LEVEL_COUNTS = { error: 1842, warn: 6310, info: 48200, debug: 21900, trace: 9120 };

Object.assign(window, {
  Icon, SOURCES, LEVELS, LEVEL_MAP, LOG_ENTRIES, BUCKETS,
  FACET_SERVICES, FACET_HOSTS, LEVEL_COUNTS, ts,
});
