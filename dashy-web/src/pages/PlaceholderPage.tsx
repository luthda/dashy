interface PlaceholderPageProps {
  title: string
  subtitle?: string
}

export function PlaceholderPage({ title, subtitle = "Coming soon" }: PlaceholderPageProps) {
  return (
    <div className="flex-1 grid place-items-center p-6">
      <div className="text-center">
        <div className="text-[15px] font-semibold mb-1">{title}</div>
        <div className="text-[12px] text-muted-foreground font-mono">{subtitle}</div>
      </div>
    </div>
  )
}
