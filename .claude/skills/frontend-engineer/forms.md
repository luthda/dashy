# Form Patterns

Reference for react-hook-form, zod schemas, and form conventions.

---

## Stack

All forms use **react-hook-form** with **zod** for validation. No other form library.

```typescript
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
```

---

## Schema Definition

Zod schemas live in `lib/schemas/` when shared, or co-located with the form component when
used only once.

```typescript
// lib/schemas/source.ts
import { z } from "zod"

// AppInsights is the only supported source type right now.
// When Loki is added, extend this schema with a discriminated union.
export const createSourceSchema = z.object({
  name: z.string().min(1, "Name is required"),
  appId: z.string().min(1, "Application ID is required"),
  apiKey: z.string().min(1, "API Key is required"),
})

export type CreateSourceFormData = z.infer<typeof createSourceSchema>
```

```typescript
// lib/schemas/alert.ts
export const createAlertSchema = z.object({
  name: z.string().min(1, "Name is required"),
  sourceId: z.string().uuid("Select a source"),
  query: z.string().min(1, "Query is required"),
  checkIntervalSeconds: z.coerce.number().min(60, "Minimum 60 seconds"),
  threshold: z.coerce.number().min(1, "Minimum threshold is 1"),
  enabled: z.boolean().default(true),
})

export type CreateAlertFormData = z.infer<typeof createAlertSchema>
```

```typescript
// lib/schemas/tag.ts
export const createTagSchema = z.object({
  name: z.string().min(1, "Name is required"),
  color: z.string().regex(/^#[0-9a-fA-F]{6}$/, "Must be a hex colour"),
  filters: z.object({
    terms: z.array(z.string()).default([]),
    levels: z.array(z.string()).default([]),
    eventTypes: z.array(z.string()).default([]),
  }),
})

export type CreateTagFormData = z.infer<typeof createTagSchema>
```

```typescript
// lib/schemas/saved-search.ts
export const createSavedSearchSchema = z.object({
  name: z.string().min(1, "Name is required"),
  sourceId: z.string().uuid("Select a source"),
  query: z.string().min(1, "Query is required"),
  tagIds: z.array(z.string().uuid()).default([]),
  timeRange: z.union([
    z.object({ type: z.literal("relative"), value: z.string() }),
    z.object({ type: z.literal("absolute"), from: z.string(), to: z.string() }),
  ]),
  refreshIntervalSeconds: z.coerce.number().min(10).nullable().default(null),
})

export type CreateSavedSearchFormData = z.infer<typeof createSavedSearchSchema>
```

---

## Form Component Pattern

```typescript
// components/sources/SourceSetupDialog.tsx
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { createSourceSchema, type CreateSourceFormData } from "@/lib/schemas/source"
import { useCreateSource } from "@/hooks/useSources"
import { useToast } from "@/hooks/use-toast"

type SourceSetupDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function SourceSetupDialog({ open, onOpenChange }: SourceSetupDialogProps) {
  const { toast } = useToast()
  const createSource = useCreateSource()

  const form = useForm<CreateSourceFormData>({
    resolver: zodResolver(createSourceSchema),
    defaultValues: {
      name: "",
      type: "app_insights",
      config: { type: "app_insights", appId: "", apiKey: "" },
    },
  })

  const onSubmit = form.handleSubmit(async (data) => {
    try {
      await createSource.mutateAsync(data)
      toast({ title: "Source created" })
      onOpenChange(false)
      form.reset()
    } catch (error) {
      toast({
        variant: "destructive",
        title: "Failed to create source",
        description: error instanceof Error ? error.message : "Unknown error",
      })
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Connect a source</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="space-y-4">
          <div>
            <Label htmlFor="name">Name</Label>
            <Input id="name" {...form.register("name")} />
            {form.formState.errors.name && (
              <p className="mt-1 text-sm text-destructive">
                {form.formState.errors.name.message}
              </p>
            )}
          </div>

          <div>
            <Label htmlFor="type">Type</Label>
            <Select
              value={form.watch("type")}
              onValueChange={(value) => form.setValue("type", value as "app_insights" | "loki")}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="app_insights">Azure App Insights</SelectItem>
                <SelectItem value="loki">Loki / Grafana</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* Conditional config fields based on type */}

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={createSource.isPending}>
              {createSource.isPending ? "Creating..." : "Create"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}
```

---

## Conditional Fields

When a form has conditional sections (e.g. source type determines config fields), use
`form.watch()` to read the discriminant and render the right fields.

```typescript
const sourceType = form.watch("type")

{sourceType === "app_insights" && (
  <>
    <Input {...form.register("config.appId")} placeholder="App ID" />
    <Input {...form.register("config.apiKey")} placeholder="API Key" type="password" />
  </>
)}

{sourceType === "loki" && (
  <>
    <Input {...form.register("config.baseUrl")} placeholder="http://loki:3100" />
    <Input {...form.register("config.orgId")} placeholder="Org ID (optional)" />
    <Input {...form.register("config.authToken")} placeholder="Auth token (optional)" type="password" />
  </>
)}
```

---

## Edit Forms

For edit forms, pass the existing entity as `defaultValues` and use the update mutation.

```typescript
const form = useForm<CreateAlertFormData>({
  resolver: zodResolver(createAlertSchema),
  defaultValues: {
    name: alert.name,
    sourceId: alert.sourceId,
    query: alert.query,
    checkIntervalSeconds: alert.checkIntervalSeconds,
    threshold: alert.threshold,
    enabled: alert.enabled,
  },
})
```

Reuse the same schema for create and edit when the shape is identical.

---

## Form in Dialog Pattern

Forms that live inside dialogs follow this pattern:

1. Dialog `open` state controlled by the parent.
2. `form.reset()` on successful submit or dialog close.
3. Submit button disabled while mutation is pending.
4. Toast on success, toast on error — both handled in the form component.

---

## Inline Validation Display

Show validation errors immediately below the field. Use the shadcn destructive text style.

```typescript
{form.formState.errors.name && (
  <p className="mt-1 text-sm text-destructive">
    {form.formState.errors.name.message}
  </p>
)}
```

---

## Rules

1. **Every form uses zod + react-hook-form** — no uncontrolled forms, no manual validation.
2. **Schema co-location** — define the zod schema at the top of the component file. Move to `lib/schemas/` only if the same schema is needed in multiple files.
3. **`z.coerce.number()`** for numeric fields from text inputs.
4. **No `any`** in form types — always `z.infer<typeof schema>`.
5. **Reset on close** — dialogs must reset form state when closed.
6. **Disable submit while pending** — prevent double submissions.
7. **Sensitive fields** — use `type="password"` for API keys and tokens.
