interface FieldProps {
  label: string
  hint?: string
  error?: string
  children: React.ReactNode
}

export function Field({ label, hint, error, children }: FieldProps) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="text-muted-foreground text-[12.5px] font-medium">
        {label}
        {hint && <span className="ml-1 font-normal opacity-60">({hint})</span>}
      </span>
      {children}
      {error && <span className="text-[11.5px] text-[var(--sev-error)]">{error}</span>}
    </label>
  )
}

export const inputCls =
  "h-9 px-3 rounded-md border border-border bg-background text-[13.5px] outline-none focus:ring-2 focus:ring-ring transition-shadow placeholder:text-muted-foreground/60"
