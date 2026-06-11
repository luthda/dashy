import { useState, useEffect } from "react"
import { Outlet } from "react-router-dom"
import { Toaster } from "sonner"
import { useAlertStream } from "@/hooks/useAlertStream"
import { Sidebar } from "./Sidebar"
import { Topbar } from "./Topbar"

export function AppShell() {
  const [theme, setTheme] = useState<"light" | "dark">(() => {
    const stored = localStorage.getItem("dashy-theme")
    if (stored === "light" || stored === "dark") return stored
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"
  })

  const [collapsed, setCollapsed] = useState(false)

  // Mounted once here so alert toasts appear on every page.
  useAlertStream()

  useEffect(() => {
    document.documentElement.classList.toggle("dark", theme === "dark")
    localStorage.setItem("dashy-theme", theme)
  }, [theme])

  return (
    <div className="flex h-full overflow-hidden">
      <Sidebar collapsed={collapsed} />
      <div className="flex flex-col flex-1 min-w-0 overflow-hidden">
        <Topbar
          theme={theme}
          onToggleTheme={() => setTheme((t) => (t === "dark" ? "light" : "dark"))}
          onToggleSidebar={() => setCollapsed((c) => !c)}
        />
        <main className="flex-1 overflow-hidden bg-background flex flex-col min-h-0">
          <Outlet />
        </main>
      </div>
      {/* sonner sizes toasts via its --width CSS variable, so width must be a
          style value; font and padding go through Tailwind classes. */}
      <Toaster
        theme={theme}
        position="top-center"
        expand
        style={{ "--width": "440px" } as React.CSSProperties}
        toastOptions={{ classNames: { toast: "p-4! text-sm!" } }}
      />
    </div>
  )
}
