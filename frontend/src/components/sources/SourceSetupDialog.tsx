import { cn } from "@/lib/utils"
import { useCreateSource, useTestConnection, useUpdateSource } from "@/hooks/useSources"
import { SourceType, type Source } from "@/lib/types"
import { zodResolver } from "@hookform/resolvers/zod"
import { CheckCircle2Icon, Loader2Icon, XCircleIcon } from "lucide-react"
import { useForm } from "react-hook-form"
import { z } from "zod"

// ── Create schemas: credentials required ────────────────────────────────────
const appInsightsCreate = z.object({
  name: z.string().min(1, "Name is required"),
  type: z.literal(SourceType.AppInsights),
  appId: z.string().min(1, "Application ID is required"),
  apiKey: z.string().min(1, "API Key is required"),
})

const lokiCreate = z.object({
  name: z.string().min(1, "Name is required"),
  type: z.literal(SourceType.Loki),
  baseUrl: z.url("Must be a valid URL"),
  orgId: z.string().optional(),
  authToken: z.string().optional(),
})

// ── Edit schemas: credentials optional (blank = keep existing) ───────────────
const appInsightsEdit = z.object({
  name: z.string().min(1, "Name is required"),
  type: z.literal(SourceType.AppInsights),
  appId: z.string().optional(),
  apiKey: z.string().optional(),
})

const lokiEdit = z.object({
  name: z.string().min(1, "Name is required"),
  type: z.literal(SourceType.Loki),
  baseUrl: z.string().optional(),
  orgId: z.string().optional(),
  authToken: z.string().optional(),
})

const createSourceSchema = z.discriminatedUnion("type", [appInsightsCreate, lokiCreate])
const editSourceSchema = z.discriminatedUnion("type", [appInsightsEdit, lokiEdit])

type CreateSourceForm = {
  name: string
  type: SourceType
  appId?: string
  apiKey?: string
  baseUrl?: string
  orgId?: string
  authToken?: string
}

interface SourceSetupDialogProps {
  source?: Source
  onClose: () => void
}

export function SourceSetupDialog({ source, onClose }: SourceSetupDialogProps) {
  const isEditing = Boolean(source)

  const form = useForm<CreateSourceForm>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver((isEditing ? editSourceSchema : createSourceSchema) as any),
    defaultValues: {
      name: source?.name ?? "",
      type: (source?.type as SourceType) ?? SourceType.AppInsights,
      appId: "",
      apiKey: "",
    } as CreateSourceForm,
  })

  const watchedType = form.watch("type")

  const create = useCreateSource()
  const update = useUpdateSource()
  const test = useTestConnection()

  const isSubmitting = create.isPending || update.isPending
  const submitError = create.error?.message ?? update.error?.message

  const fieldError = (name: string) =>
    (form.formState.errors as Record<string, { message?: string }>)[name]?.message

  function buildConfig(data: CreateSourceForm) {
    if (data.type === SourceType.AppInsights) {
      return { appId: data.appId, apiKey: data.apiKey }
    }
    return {
      baseUrl: data.baseUrl,
      orgId: data.orgId || undefined,
      authToken: data.authToken || undefined,
    }
  }

  // Edit mode: blank credentials → return undefined so the server keeps the
  // existing encrypted config untouched. Any value entered replaces the whole blob.
  function buildEditConfig(data: CreateSourceForm): object | undefined {
    if (data.type === SourceType.AppInsights) {
      const appId = data.appId?.trim() ?? ""
      const apiKey = data.apiKey?.trim() ?? ""
      if (!appId && !apiKey) return undefined
      return { appId, apiKey }
    }
    const baseUrl = data.baseUrl?.trim() ?? ""
    const orgId = data.orgId?.trim() || undefined
    const authToken = data.authToken?.trim() || undefined
    if (!baseUrl && !orgId && !authToken) return undefined
    return { baseUrl, orgId, authToken }
  }

  async function onSubmit(data: CreateSourceForm) {
    if (isEditing && source) {
      // Validate "all or nothing" so we never store a half-filled credential blob.
      if (data.type === SourceType.AppInsights) {
        const appId = data.appId?.trim() ?? ""
        const apiKey = data.apiKey?.trim() ?? ""
        if (Boolean(appId) !== Boolean(apiKey)) {
          form.setError("appId", {
            message: "Enter both Application ID and API Key, or leave both blank to keep current",
          })
          return
        }
      } else {
        const baseUrl = data.baseUrl?.trim() ?? ""
        const others = (data.orgId?.trim() || data.authToken?.trim()) ?? ""
        if (!baseUrl && others) {
          form.setError("baseUrl", {
            message: "Base URL is required to change Loki credentials",
          })
          return
        }
      }

      await update.mutateAsync({
        id: source.id,
        name: data.name,
        config: buildEditConfig(data),
      })
    } else {
      await create.mutateAsync({ name: data.name, type: data.type, config: buildConfig(data) })
    }
    onClose()
  }

  function handleTypeChange(newType: SourceType) {
    if (newType === SourceType.AppInsights) {
      form.reset({ name: form.getValues("name"), type: newType, appId: "", apiKey: "" })
    } else {
      form.reset({
        name: form.getValues("name"),
        type: newType,
        baseUrl: "",
        orgId: "",
        authToken: "",
      })
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm">
      <div className="bg-card border-border mx-4 w-full max-w-md rounded-xl border p-6 shadow-xl">
        <h2 className="mb-5 text-[15px] font-semibold">
          {isEditing ? "Edit source" : "Add source"}
        </h2>

        <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4">
          <Field label="Name" error={form.formState.errors.name?.message}>
            <input {...form.register("name")} placeholder="My App Insights" className={inputCls} />
          </Field>

          {!isEditing && (
            <Field label="Type">
              <div className="flex gap-2">
                {([SourceType.AppInsights, SourceType.Loki] as const).map((t) => (
                  <button
                    key={t}
                    type="button"
                    onClick={() => handleTypeChange(t)}
                    className={cn(
                      "h-9 flex-1 rounded-md border text-[13px] font-medium transition-colors",
                      watchedType === t
                        ? "border-primary bg-primary/10 text-foreground"
                        : "border-border text-muted-foreground hover:border-primary/50",
                    )}
                  >
                    {t === SourceType.AppInsights ? "Azure App Insights" : "Grafana Loki"}
                  </button>
                ))}
              </div>
            </Field>
          )}

          {watchedType === SourceType.AppInsights ? (
            <>
              <Field label="Application ID" error={fieldError("appId")}>
                <input
                  {...form.register("appId")}
                  placeholder={
                    isEditing
                      ? "Leave blank to keep current"
                      : "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                  }
                  className={inputCls}
                />
              </Field>
              <Field label="API Key (Read telemetry)" error={fieldError("apiKey")}>
                <input
                  {...form.register("apiKey")}
                  type="password"
                  placeholder={isEditing ? "Leave blank to keep current" : "Paste API key"}
                  className={inputCls}
                />
              </Field>
              <p className="text-muted-foreground -mt-2 text-[11.5px]">
                {isEditing ? (
                  "Leave both blank to keep the saved credentials, or enter both to replace them."
                ) : (
                  <>
                    Azure Portal &rarr; App Insights &rarr; Configure &rarr; API Access. The
                    Application ID is the GUID on that blade &mdash; not the instrumentation key or
                    connection string.
                  </>
                )}
              </p>
            </>
          ) : (
            <>
              <Field label="Base URL" error={fieldError("baseUrl")}>
                <input
                  {...form.register("baseUrl")}
                  placeholder={isEditing ? "Leave blank to keep current" : "http://loki:3100"}
                  className={inputCls}
                />
              </Field>
              <Field label="Org ID (optional)">
                <input {...form.register("orgId")} placeholder="tenant-id" className={inputCls} />
              </Field>
              <Field label="Auth token (optional)">
                <input
                  {...form.register("authToken")}
                  type="password"
                  placeholder={isEditing ? "Leave blank to keep current" : "Bearer token"}
                  className={inputCls}
                />
              </Field>
              {isEditing && (
                <p className="text-muted-foreground -mt-2 text-[11.5px]">
                  Leave fields blank to keep the saved credentials.
                </p>
              )}
            </>
          )}

          {source && (
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => test.mutate(source.id)}
                disabled={test.isPending}
                className="text-muted-foreground hover:text-foreground text-[12.5px] underline underline-offset-2"
              >
                {test.isPending ? "Testing…" : "Test connection"}
              </button>
              {test.data?.ok === true && (
                <CheckCircle2Icon size={14} className="text-[var(--sev-info)]" />
              )}
              {test.data?.ok === false && (
                <span className="flex items-center gap-1 text-[12px] text-[var(--sev-error)]">
                  <XCircleIcon size={14} /> {test.data.error}
                </span>
              )}
            </div>
          )}

          {submitError && <p className="text-[12px] text-[var(--sev-error)]">{submitError}</p>}

          <div className="mt-1 flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="border-border hover:bg-accent h-9 rounded-md border px-4 text-[13px] transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="bg-primary text-primary-foreground flex h-9 items-center gap-2 rounded-md px-4 text-[13px] font-medium transition-opacity hover:opacity-90"
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

function Field({
  label,
  error,
  children,
}: {
  label: string
  error?: string
  children: React.ReactNode
}) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="text-muted-foreground text-[12.5px] font-medium">{label}</span>
      {children}
      {error && <span className="text-[11.5px] text-[var(--sev-error)]">{error}</span>}
    </label>
  )
}

const inputCls =
  "h-9 px-3 rounded-md border border-border bg-background text-[13.5px] outline-none focus:ring-2 focus:ring-ring transition-shadow placeholder:text-muted-foreground/60"
