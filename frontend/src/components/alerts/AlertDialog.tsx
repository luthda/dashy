import { AlertFiringHistory } from "@/components/alerts/AlertFiringHistory"
import { Field, inputCls } from "@/components/shared/Field"
import { useCreateAlert, useDeleteAlert, useUpdateAlert } from "@/hooks/useAlerts"
import { useSourcesQuery } from "@/hooks/useSources"
import { useTagsQuery } from "@/hooks/useTags"
import { cn } from "@/lib/utils"
import type { Alert } from "@/lib/types"
import { zodResolver } from "@hookform/resolvers/zod"
import { Loader2Icon, XIcon } from "lucide-react"
import { useEffect, useState } from "react"
import { useForm, useWatch } from "react-hook-form"
import { z } from "zod"

const alertSchema = z
  .object({
    name: z.string().min(1, "Name is required"),
    sourceId: z.string().min(1, "Source is required"),
    mode: z.enum(["query", "tag"]),
    query: z.string(),
    tagId: z.string(),
    threshold: z.number().int().min(1, "Threshold must be at least 1"),
    enabled: z.boolean(),
  })
  .superRefine((data, ctx) => {
    if (data.mode === "query" && !data.query.trim()) {
      ctx.addIssue({ code: "custom", path: ["query"], message: "Query is required" })
    }
    if (data.mode === "tag" && !data.tagId) {
      ctx.addIssue({ code: "custom", path: ["tagId"], message: "Tag is required" })
    }
  })

type AlertFormData = z.infer<typeof alertSchema>

interface AlertDialogProps {
  /** null = create mode */
  alert: Alert | null
  onClose: () => void
}

export function AlertDialog({ alert, onClose }: AlertDialogProps) {
  const { data: sources } = useSourcesQuery()
  const { data: tags } = useTagsQuery()
  const createAlert = useCreateAlert()
  const updateAlert = useUpdateAlert()
  const deleteAlert = useDeleteAlert()
  const [confirmDelete, setConfirmDelete] = useState(false)
  const isEditing = alert !== null

  const form = useForm<AlertFormData>({
    resolver: zodResolver(alertSchema),
    defaultValues: {
      name: alert?.name ?? "",
      sourceId: alert?.sourceId ?? sources?.[0]?.id ?? "",
      mode: "query",
      query: alert?.query ?? "",
      tagId: "",
      threshold: alert?.threshold ?? 1,
      enabled: alert?.enabled ?? true,
    },
  })

  // useWatch instead of form.watch — the latter is incompatible with React Compiler memoization
  const mode = useWatch({ control: form.control, name: "mode" })
  const isSubmitting = createAlert.isPending || updateAlert.isPending
  const mutationError = createAlert.error ?? updateAlert.error ?? deleteAlert.error

  useEffect(() => {
    function handleKey(e: KeyboardEvent) {
      if (e.key === "Escape") onClose()
    }
    document.addEventListener("keydown", handleKey)
    return () => document.removeEventListener("keydown", handleKey)
  }, [onClose])

  async function onSubmit(data: AlertFormData) {
    const body = {
      name: data.name,
      sourceId: data.sourceId,
      query: data.mode === "query" ? data.query : undefined,
      tagId: data.mode === "tag" ? data.tagId : undefined,
      threshold: data.threshold,
      enabled: data.enabled,
    }

    if (isEditing) {
      await updateAlert.mutateAsync({ id: alert.id, ...body })
    } else {
      await createAlert.mutateAsync(body)
    }
    onClose()
  }

  async function onDelete() {
    if (!isEditing) return
    if (!confirmDelete) {
      setConfirmDelete(true)
      return
    }
    await deleteAlert.mutateAsync(alert.id)
    onClose()
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm" onClick={onClose}>
      <div
        className="bg-card border-border mx-4 max-h-[85vh] w-full max-w-md overflow-y-auto rounded-xl border p-6 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-[15px] font-semibold">{isEditing ? "Edit alert" : "Create alert"}</h2>
          <button onClick={onClose} className="text-muted-foreground hover:text-foreground">
            <XIcon size={16} />
          </button>
        </div>

        <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4">
          <Field label="Name" error={form.formState.errors.name?.message}>
            <input {...form.register("name")} placeholder="e.g. Production exceptions" className={inputCls} />
          </Field>

          <Field label="Source" error={form.formState.errors.sourceId?.message}>
            <select {...form.register("sourceId")} className={inputCls}>
              {(sources ?? []).map((s) => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
          </Field>

          {/* Query / From tag mode toggle */}
          <div className="bg-muted inline-flex self-start rounded-lg p-0.75">
            {(["query", "tag"] as const).map((m) => (
              <button
                key={m}
                type="button"
                onClick={() => form.setValue("mode", m)}
                className={cn(
                  "rounded-md px-2.5 py-1.5 text-xs font-medium",
                  mode === m
                    ? "bg-background text-foreground shadow-sm"
                    : "text-muted-foreground hover:text-foreground",
                )}
              >
                {m === "query" ? "Query" : "From tag"}
              </button>
            ))}
          </div>

          {mode === "query" ? (
            <Field label="Query" hint="KQL" error={form.formState.errors.query?.message}>
              <textarea
                {...form.register("query")}
                rows={4}
                spellCheck={false}
                placeholder={'traces | where severityLevel >= 3'}
                className={cn(inputCls, "h-auto min-h-20 resize-y py-2 font-mono text-[12.5px]")}
              />
            </Field>
          ) : (
            <Field
              label="Tag"
              hint="filters are resolved into a query on save"
              error={form.formState.errors.tagId?.message}
            >
              <select {...form.register("tagId")} className={inputCls}>
                <option value="">Select a tag…</option>
                {(tags ?? []).map((t) => (
                  <option key={t.id} value={t.id}>{t.name}</option>
                ))}
              </select>
            </Field>
          )}

          <Field label="Threshold" hint="fires at ≥ N results per check" error={form.formState.errors.threshold?.message}>
            <input
              type="number"
              min={1}
              {...form.register("threshold", { valueAsNumber: true })}
              className={inputCls}
            />
          </Field>

          <label className="flex items-center gap-2 text-[13px]">
            <input type="checkbox" {...form.register("enabled")} className="accent-primary h-4 w-4" />
            Enabled
          </label>

          {mutationError && (
            <p className="text-[12px] text-[var(--sev-error)]">{mutationError.message}</p>
          )}

          <div className="mt-1 flex items-center gap-2">
            {isEditing && (
              <button
                type="button"
                onClick={onDelete}
                disabled={deleteAlert.isPending}
                className={cn(
                  "h-9 rounded-md border px-3 text-[13px] transition-colors",
                  confirmDelete
                    ? "border-[var(--sev-error)] text-[var(--sev-error)] hover:bg-[var(--sev-error)]/10"
                    : "border-border text-muted-foreground hover:text-[var(--sev-error)]",
                )}
              >
                {deleteAlert.isPending ? "Deleting…" : confirmDelete ? "Confirm delete?" : "Delete"}
              </button>
            )}
            <div className="flex flex-1 justify-end gap-2">
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
                {isEditing ? "Save" : "Create"}
              </button>
            </div>
          </div>
        </form>

        {isEditing && (
          <div className="border-border mt-6 border-t pt-4">
            <h3 className="text-muted-foreground mb-2 text-[12.5px] font-medium">Firing history</h3>
            <AlertFiringHistory alertId={alert.id} />
          </div>
        )}
      </div>
    </div>
  )
}
