using Dashy.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dashy.Api.Data.Configurations;

public class SavedSearchConfiguration : IEntityTypeConfiguration<SavedSearch>
{
    public void Configure(EntityTypeBuilder<SavedSearch> builder)
    {
        builder.ToTable("saved_searches");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.SourceId).HasColumnName("source_id");

        builder.HasOne(s => s.Source)
            .WithMany()
            .HasForeignKey(s => s.SourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.Query).HasColumnName("query").IsRequired();

        builder.Property(s => s.TagIds)
            .HasColumnName("tag_ids")
            .HasDefaultValue("[]");

        builder.Property(s => s.TimeRange)
            .HasColumnName("time_range")
            .HasDefaultValue("""{"type":"relative","value":"1h"}""");

        builder.Property(s => s.RefreshIntervalSeconds).HasColumnName("refresh_interval_seconds");

        builder.Property(s => s.IsBroken)
            .HasColumnName("is_broken")
            .HasDefaultValue(false);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("datetime('now')");
    }
}
