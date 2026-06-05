import { Field, inputCls } from "@/components/shared/Field"
import { useCreateSource, useTestConnection, useUpdateSource } from "@/hooks/useSources"
import { SourceType, type Source } from "@/lib/types"
import { zodResolver } from "@hookform/resolvers/zod"
import { CheckCircle2Icon, Loader2Icon, XCircleIcon } from "lucide-react"
import { useForm, type Resolver } from "react-hook-form"
import { z } from "zod"

type SourceForm = {
  name: string
  appId: string
  apiKey: string
}

const createSchema = z.object({
  name: z.string().min(1, "Name is required"),
  appId: z.string().min(1, "Application ID is required"),
  apiKey: z.string().min(1, "API Key is required"),
})

// Edit mode allows the credential fields to be left blank (keep saved values),
// but they stay required `string`s so the form type matches createSchema.
const editSchema = z.object({
  name: z.string().min(1, "Name is required"),
  appId: z.string(),
  apiKey: z.string(),
})

interface SourceSetupDialogProps {
  source?: Source
  onClose: () => void
}

export function SourceSetupDialog({ source, onClose }: SourceSetupDialogProps) {
  const isEditing = Boolean(source)

  const schema = isEditing ? editSchema : createSchema
  const form = useForm<SourceForm>({
    resolver: zodResolver(schema) as Resolver<SourceForm>,
    defaultValues: {
      name: source?.name ?? "",
      appId: "",
      apiKey: "",
    },
  })

  const create = useCreateSource()
  const update = useUpdateSource()
  const test = useTestConnection()

  const isSubmitting = create.isPending || update.isPending
  const submitError = create.error?.message ?? update.error?.message

  async function onSubmit(data: SourceForm) {
    if (isEditing && source) {
      const appId = data.appId?.trim() ?? ""
      const apiKey = data.apiKey?.trim() ?? ""
      if (Boolean(appId) !== Boolean(apiKey)) {
        form.setError("appId", {
          message: "Enter both Application ID and API Key, or leave both blank to keep current",
        })
        return
      }

      const config = appId && apiKey ? { appId, apiKey } : undefined
      await update.mutateAsync({ id: source.id, name: data.name, config })
    } else {
      await create.mutateAsync({
        name: data.name,
        type: SourceType.AppInsights,
        config: { appId: data.appId, apiKey: data.apiKey },
      })
    }
    onClose()
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

          <Field label="Application ID" error={form.formState.errors.appId?.message}>
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
          <Field label="API Key (Read telemetry)" error={form.formState.errors.apiKey?.message}>
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

