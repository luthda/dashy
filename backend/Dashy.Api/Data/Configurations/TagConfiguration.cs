using Dashy.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dashy.Api.Data.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Color)
            .HasColumnName("color")
            .HasMaxLength(20)
            .HasDefaultValue("#6366f1");

        builder.Property(t => t.Filters)
            .HasColumnName("filters")
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("datetime('now')");
    }
}
