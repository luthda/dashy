import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom"
import { AppShell } from "@/components/layout/AppShell"
import { LogsPage } from "@/pages/LogsPage"
import { PlaceholderPage } from "@/pages/PlaceholderPage"
import { SettingsSourcesPage } from "@/pages/SettingsSourcesPage"

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30_000,
    },
  },
})

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<Navigate to="/logs" replace />} />
            <Route path="/logs"    element={<LogsPage />} />
            <Route path="/metrics" element={<PlaceholderPage title="Metrics" subtitle="Focusing on Logs first" />} />
            <Route path="/traces"  element={<PlaceholderPage title="Traces"  subtitle="Focusing on Logs first" />} />
            <Route path="/alerts"  element={<PlaceholderPage title="Alerts"  subtitle="Coming soon" />} />
            <Route path="/settings/sources" element={<SettingsSourcesPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
