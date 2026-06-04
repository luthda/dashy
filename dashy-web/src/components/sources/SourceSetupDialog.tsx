import { cn } from "@/lib/utils"
import { useCreateSource, useTestConnection, useUpdateSource } from "@/hooks/useSources"
import type { Source } from "@/lib/types"
import { CheckCircle2Icon, Loader2Icon, XCircleIcon } from "lucide-react"
import { useState } from "react"

interface SourceSetupDialogProps {
  source?: Source
  onClose: () => void
}

type SourceType = "AppInsights" | "Loki"

export function SourceSetupDialog({ source, onClose }: SourceSetupDialogProps) {
  const isEditing = Boolean(source)
  const [name, setName] = useState(source?.name ?? "")
  const [type, setType] = useState<SourceType>(
    (source?.type as SourceType) ?? "AppInsights",
  )

  // App Insights fields
  const [appId, setAppId] = useState("")
  const [apiKey, setApiKey] = useState("")

  // Loki fields
  const [baseUrl, setBaseUrl] = useState("")
  const [orgId, setOrgId] = useState("")
  const [authToken, setAuthToken] = useState("")

  const create = useCreateSource()
  const update = useUpdateSource()
  const test   = useTestConnection()

  const isSubmitting = create.isPending || update.isPending
  const submitError  = create.error?.message ?? update.error?.message

  function buildConfig() {
    if (type === "AppInsights") return { appId, apiKey }
    return { baseUrl, orgId: orgId || undefined, authToken: authToken || undefined }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const config = buildConfig()

    if (isEditing && source) {
      await update.mutateAsync({ id: source.id, name, config })
    } else {
      await create.mutateAsync({ name, type, config })
    }
    onClose()
  }

  async function handleTest() {
    if (!source) return
    test.mutate(source.id)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm">
      <div className="bg-card border border-border rounded-xl shadow-xl w-full max-w-md mx-4 p-6">
        <h2 className="text-[15px] font-semibold mb-5">
          {isEditing ? "Edit source" : "Add source"}
        </h2>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          {/* Name */}
          <Field label="Name">
            <input
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="My App Insights"
              className={inputCls}
            />
          </Field>

          {/* Type selector */}
          {!isEditing && (
            <Field label="Type">
              <div className="flex gap-2">
                {(["AppInsights", "Loki"] as const).map((t) => (
                  <button
                    key={t}
                    type="button"
                    onClick={() => setType(t)}
                    className={cn(
                      "flex-1 h-9 rounded-md border text-[13px] font-medium transition-colors",
                      type === t
                        ? "border-primary bg-primary/10 text-foreground"
                        : "border-border text-muted-foreground hover:border-primary/50",
                    )}
                  >
                    {t === "AppInsights" ? "Azure App Insights" : "Grafana Loki"}
                  </button>
                ))}
              </div>
            </Field>
          )}

          {/* Credential fields */}
          {type === "AppInsights" ? (
            <>
              <Field label="Application ID">
                <input
                  required={!isEditing}
                  value={appId}
                  onChange={(e) => setAppId(e.target.value)}
                  placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                  className={inputCls}
                />
              </Field>
              <Field label="API Key (Read telemetry)">
                <input
                  required={!isEditing}
                  type="password"
                  value={apiKey}
                  onChange={(e) => setApiKey(e.target.value)}
                  placeholder="Paste API key"
                  className={inputCls}
                />
              </Field>
              <p className="text-[11.5px] text-muted-foreground -mt-2">
                Azure Portal → App Insights → Configure → API Access → Create API key
              </p>
            </>
          ) : (
            <>
              <Field label="Base URL">
                <input
                  required={!isEditing}
                  value={baseUrl}
                  onChange={(e) => setBaseUrl(e.target.value)}
                  placeholder="http://loki:3100"
                  className={inputCls}
                />
              </Field>
              <Field label="Org ID (optional)">
                <input
                  value={orgId}
                  onChange={(e) => setOrgId(e.target.value)}
                  placeholder="tenant-id"
                  className={inputCls}
                />
              </Field>
              <Field label="Auth token (optional)">
                <input
                  type="password"
                  value={authToken}
                  onChange={(e) => setAuthToken(e.target.value)}
                  placeholder="Bearer token"
                  className={inputCls}
                />
              </Field>
            </>
          )}

          {/* Test connection result */}
          {source && (
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={handleTest}
                disabled={test.isPending}
                className="text-[12.5px] text-muted-foreground hover:text-foreground underline underline-offset-2"
              >
                {test.isPending ? "Testing…" : "Test connection"}
              </button>
              {test.data?.ok === true  && <CheckCircle2Icon size={14} className="text-[var(--sev-info)]" />}
              {test.data?.ok === false && (
                <span className="text-[12px] text-[var(--sev-error)] flex items-center gap-1">
                  <XCircleIcon size={14} /> {test.data.error}
                </span>
              )}
            </div>
          )}

          {submitError && (
            <p className="text-[12px] text-[var(--sev-error)]">{submitError}</p>
          )}

          <div className="flex justify-end gap-2 mt-1">
            <button
              type="button"
              onClick={onClose}
              className="h-9 px-4 rounded-md border border-border text-[13px] hover:bg-accent transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="h-9 px-4 rounded-md bg-primary text-primary-foreground text-[13px] font-medium hover:opacity-90 transition-opacity flex items-center gap-2"
            >
              {isSubmitting && <Loader2Icon size={13} className="animate-spin" />}
              {isEditing ? "Save" : "Add source"}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="text-[12.5px] font-medium text-muted-foreground">{label}</span>
      {children}
    </label>
  )
}

const inputCls =
  "h-9 px-3 rounded-md border border-border bg-background text-[13.5px] outline-none focus:ring-2 focus:ring-ring transition-shadow placeholder:text-muted-foreground/60"
