import { cn } from "@/lib/utils"
import { Field, inputCls } from "@/components/shared/Field"
import { useCreateTag, useDeleteTag, useTagsQuery, useUpdateTag } from "@/hooks/useTags"
import { LEVELS, EVENT_TYPES, type Tag } from "@/lib/types"
import { zodResolver } from "@hookform/resolvers/zod"
import { Loader2Icon, PencilIcon, PlusIcon, Trash2Icon, XIcon } from "lucide-react"
import { useState } from "react"
import { useForm } from "react-hook-form"
import { z } from "zod"

const TAG_COLORS = [
  "#6366f1", "#ef4444", "#f59e0b", "#22c55e", "#06b6d4",
  "#ec4899", "#8b5cf6", "#f97316",
]

const tagSchema = z.object({
  name: z.string().min(1, "Name is required"),
  color: z.string(),
  terms: z.string(),
  levels: z.array(z.string()),
  eventTypes: z.array(z.string()),
})

type TagFormData = z.infer<typeof tagSchema>

interface TagsDialogProps {
  onClose: () => void
}

export function TagsDialog({ onClose }: TagsDialogProps) {
  const { data: tags } = useTagsQuery()
  const [editing, setEditing] = useState<Tag | null>(null)
  const [showForm, setShowForm] = useState(false)

  function handleEdit(tag: Tag) {
    setEditing(tag)
    setShowForm(true)
  }

  function handleNew() {
    setEditing(null)
    setShowForm(true)
  }

  function handleFormDone() {
    setShowForm(false)
    setEditing(null)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm">
      <div className="bg-card border-border mx-4 w-full max-w-md rounded-xl border p-6 shadow-xl">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-[15px] font-semibold">Tags</h2>
          <button onClick={onClose} className="text-muted-foreground hover:text-foreground">
            <XIcon size={16} />
          </button>
        </div>

        {showForm ? (
          <TagForm tag={editing} onDone={handleFormDone} />
        ) : (
          <>
            <TagList tags={tags ?? []} onEdit={handleEdit} />
            <button
              onClick={handleNew}
              className="text-muted-foreground hover:text-foreground mt-3 flex items-center gap-1.5 text-[12.5px]"
            >
              <PlusIcon size={13} /> New tag
            </button>
          </>
        )}
      </div>
    </div>
  )
}

function TagList({ tags, onEdit }: { tags: Tag[]; onEdit: (tag: Tag) => void }) {
  const deleteTag = useDeleteTag()

  if (tags.length === 0) {
    return (
      <p className="text-muted-foreground py-6 text-center text-[13px]">
        No tags yet. Create one to save filter combinations.
      </p>
    )
  }

  return (
    <div className="flex flex-col gap-1.5">
      {tags.map((tag) => (
        <div
          key={tag.id}
          className="border-border flex items-center gap-2.5 rounded-lg border px-3 py-2"
        >
          <span className="h-3 w-3 rounded" style={{ background: tag.color }} />
          <span className="flex-1 text-[13px] font-medium">{tag.name}</span>
          <FilterSummary tag={tag} />
          <button
            onClick={() => onEdit(tag)}
            className="text-muted-foreground hover:text-foreground"
          >
            <PencilIcon size={13} />
          </button>
          <button
            onClick={() => deleteTag.mutate(tag.id)}
            disabled={deleteTag.isPending}
            className="text-muted-foreground hover:text-[var(--sev-error)]"
          >
            <Trash2Icon size={13} />
          </button>
        </div>
      ))}
    </div>
  )
}

function FilterSummary({ tag }: { tag: Tag }) {
  const parts: string[] = []
  if (tag.filters.terms.length > 0) parts.push(`${tag.filters.terms.length} term${tag.filters.terms.length > 1 ? "s" : ""}`)
  if (tag.filters.levels.length > 0) parts.push(`${tag.filters.levels.length} level${tag.filters.levels.length > 1 ? "s" : ""}`)
  if (tag.filters.eventTypes.length > 0) parts.push(`${tag.filters.eventTypes.length} type${tag.filters.eventTypes.length > 1 ? "s" : ""}`)

  if (parts.length === 0) return null

  return (
    <span className="text-muted-foreground text-[11px]">{parts.join(", ")}</span>
  )
}

function TagForm({ tag, onDone }: { tag: Tag | null; onDone: () => void }) {
  const createTag = useCreateTag()
  const updateTag = useUpdateTag()
  const isEditing = tag !== null

  const form = useForm<TagFormData>({
    resolver: zodResolver(tagSchema),
    defaultValues: {
      name: tag?.name ?? "",
      color: tag?.color ?? TAG_COLORS[0],
      terms: tag?.filters.terms.join(", ") ?? "",
      levels: tag?.filters.levels ?? [],
      eventTypes: tag?.filters.eventTypes ?? [],
    },
  })

  const isSubmitting = createTag.isPending || updateTag.isPending
  const watchedColor = form.watch("color")
  const watchedLevels = form.watch("levels")
  const watchedEventTypes = form.watch("eventTypes")

  async function onSubmit(data: TagFormData) {
    const filters = {
      terms: data.terms.split(",").map((s) => s.trim()).filter(Boolean),
      levels: data.levels,
      eventTypes: data.eventTypes,
    }

    if (isEditing) {
      await updateTag.mutateAsync({ id: tag.id, name: data.name, color: data.color, filters })
    } else {
      await createTag.mutateAsync({ name: data.name, color: data.color, filters })
    }
    onDone()
  }

  function toggleArrayField(field: "levels" | "eventTypes", value: string) {
    const current = form.getValues(field)
    if (current.includes(value)) {
      form.setValue(field, current.filter((v) => v !== value))
    } else {
      form.setValue(field, [...current, value])
    }
  }

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4">
      <Field label="Name" error={form.formState.errors.name?.message}>
        <input {...form.register("name")} placeholder="e.g. Production Errors" className={inputCls} />
      </Field>

      <Field label="Color">
        <div className="flex gap-1.5">
          {TAG_COLORS.map((c) => (
            <button
              key={c}
              type="button"
              onClick={() => form.setValue("color", c)}
              className={cn(
                "h-6 w-6 rounded-full border-2 transition-transform",
                watchedColor === c ? "scale-110 border-foreground" : "border-transparent",
              )}
              style={{ background: c }}
            />
          ))}
        </div>
      </Field>

      <Field label="Search terms" hint="Comma-separated">
        <input
          {...form.register("terms")}
          placeholder="e.g. prod, critical"
          className={inputCls}
        />
      </Field>

      <Field label="Levels">
        <div className="flex flex-wrap gap-1.5">
          {LEVELS.map((l) => (
            <button
              key={l.id}
              type="button"
              onClick={() => toggleArrayField("levels", l.id)}
              className={cn(
                "h-6 rounded-full border px-2.5 text-[11.5px] font-medium transition-all",
                watchedLevels.includes(l.id)
                  ? "border-border bg-card"
                  : "border-transparent opacity-40",
              )}
            >
              <span className="mr-1 inline-block h-2 w-2 rounded-sm" style={{ background: l.color }} />
              {l.label}
            </button>
          ))}
        </div>
      </Field>

      <Field label="Event types">
        <div className="flex flex-wrap gap-1.5">
          {EVENT_TYPES.map((e) => (
            <button
              key={e.id}
              type="button"
              onClick={() => toggleArrayField("eventTypes", e.id)}
              className={cn(
                "h-6 rounded-full border px-2.5 text-[11.5px] font-medium transition-all",
                watchedEventTypes.includes(e.id)
                  ? "border-border bg-card"
                  : "border-transparent opacity-40",
              )}
            >
              <span className="mr-1 inline-block h-2 w-2 rounded-sm" style={{ background: e.color }} />
              {e.label}
            </button>
          ))}
        </div>
      </Field>

      {(createTag.error || updateTag.error) && (
        <p className="text-[12px] text-[var(--sev-error)]">
          {createTag.error?.message ?? updateTag.error?.message}
        </p>
      )}

      <div className="mt-1 flex justify-end gap-2">
        <button
          type="button"
          onClick={onDone}
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
    </form>
  )
}

