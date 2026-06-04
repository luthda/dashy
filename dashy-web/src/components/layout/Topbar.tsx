import { cn } from "@/lib/utils"
import { MoonIcon, PanelLeftIcon, SunIcon } from "lucide-react"
import { useLocation } from "react-router-dom"

const PAGE_TITLE: Record<string, string> = {
  "/logs": "Logs",
  "/metrics": "Metrics",
  "/traces": "Traces",
  "/alerts": "Alerts",
  "/settings/sources": "Sources",
}

interface TopbarProps {
  theme: "light" | "dark"
  onToggleTheme: () => void
  onToggleSidebar: () => void
}

export function Topbar({ theme, onToggleTheme, onToggleSidebar }: TopbarProps) {
  const { pathname } = useLocation()
  const title = PAGE_TITLE[pathname] ?? "Dashy"

  return (
    <header className="h-14 flex-shrink-0 border-b border-border flex items-center gap-3 px-5 bg-background sticky top-0 z-10">
      <button
        onClick={onToggleSidebar}
        className={cn(
          "h-8 w-8 rounded-md grid place-items-center transition-colors",
          "text-muted-foreground hover:text-foreground hover:bg-accent",
        )}
        aria-label="Toggle sidebar"
      >
        <PanelLeftIcon size={17} />
      </button>

      <span className="font-semibold text-[14px] tracking-tight">{title}</span>

      <div className="ml-auto flex items-center gap-2">
        <button
          onClick={onToggleTheme}
          className={cn(
            "h-8 w-8 rounded-md grid place-items-center transition-colors",
            "text-muted-foreground hover:text-foreground hover:bg-accent",
          )}
          aria-label="Toggle theme"
        >
          {theme === "dark" ? <SunIcon size={17} /> : <MoonIcon size={17} />}
        </button>

        <div className="w-7 h-7 rounded-full bg-primary text-primary-foreground grid place-items-center text-[11.5px] font-semibold">
          SD
        </div>
      </div>
    </header>
  )
}
