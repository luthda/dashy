import { useAlertsQuery } from "@/hooks/useAlerts"
import { cn } from "@/lib/utils"
import {
  BellIcon,
  ListIcon,
  SettingsIcon,
} from "lucide-react"
import { NavLink } from "react-router-dom"

const NAV = [
  { to: "/logs", label: "Logs", Icon: ListIcon },
  { to: "/alerts", label: "Alerts", Icon: BellIcon },
]

interface SidebarProps {
  collapsed: boolean
}

export function Sidebar({ collapsed }: SidebarProps) {
  const { data: alerts } = useAlertsQuery()
  const hasFiring = alerts?.some((a) => a.status === "Firing") ?? false

  return (
    <aside
      className={cn(
        "flex flex-col h-full flex-shrink-0 transition-all duration-200 overflow-hidden",
        "bg-[var(--sidebar)] border-r border-[var(--sidebar-border)]",
        collapsed ? "w-0" : "w-52.5",
      )}
    >
      {/* Brand */}
      <div className="h-14 flex items-center gap-2.5 px-4 flex-shrink-0">
        <img src="/favicon.svg" alt="" className="w-6 h-6 flex-shrink-0" />
        <span className="font-semibold text-[15px] tracking-tight">Dashy</span>
      </div>

      {/* Navigation */}
      <nav className="flex-1 px-3 py-1.5 flex flex-col gap-0.5">
        {NAV.map(({ to, label, Icon }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) =>
              cn(
                "flex items-center gap-2.5 h-9 px-2.5 rounded-md text-[13.5px] font-medium transition-colors",
                isActive
                  ? "bg-[var(--sidebar-accent)] text-foreground"
                  : "text-muted-foreground hover:text-foreground hover:bg-[var(--sidebar-accent)]",
              )
            }
          >
            {({ isActive }) => (
              <>
                <span className="relative flex">
                  <Icon size={16} strokeWidth={isActive ? 2.2 : 2} />
                  {/* Orange dot on the bell while any alert is firing */}
                  {to === "/alerts" && hasFiring && (
                    <span className="absolute -top-0.5 -right-0.5 h-2 w-2 rounded-full bg-[var(--sev-warn)]" />
                  )}
                </span>
                {label}
              </>
            )}
          </NavLink>
        ))}
      </nav>

      {/* Footer */}
      <div className="p-3 border-t border-[var(--sidebar-border)]">
        <NavLink
          to="/settings/sources"
          className={({ isActive }) =>
            cn(
              "flex items-center gap-2.5 h-9 px-2.5 rounded-md text-[13.5px] font-medium transition-colors",
              isActive
                ? "bg-[var(--sidebar-accent)] text-foreground"
                : "text-muted-foreground hover:text-foreground hover:bg-[var(--sidebar-accent)]",
            )
          }
        >
          <SettingsIcon size={16} strokeWidth={2} />
          Settings
        </NavLink>
      </div>
    </aside>
  )
}
