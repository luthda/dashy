using Dashy.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dashy.Api.Infrastructure.Persistence.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.SourceId).HasColumnName("source_id");

        builder.HasOne(a => a.Source)
            .WithMany()
            .HasForeignKey(a => a.SourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.Query).HasColumnName("query").IsRequired();

        builder.Property(a => a.Threshold)
            .HasColumnName("threshold")
            .HasDefaultValue(1);

        builder.Property(a => a.Enabled)
            .HasColumnName("enabled")
            .HasDefaultValue(true);

        builder.Property(a => a.LastCheckedAt).HasColumnName("last_checked_at");

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasDefaultValue(AlertStatus.Ok);

        builder.Property(a => a.ResolvedAt).HasColumnName("resolved_at");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("datetime('now')");
    }
}
