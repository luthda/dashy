using Dashy.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dashy.Api.Infrastructure.Persistence.Configurations;

public class AlertFiringConfiguration : IEntityTypeConfiguration<AlertFiring>
{
    public void Configure(EntityTypeBuilder<AlertFiring> builder)
    {
        builder.ToTable("alert_firings");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");

        builder.Property(f => f.AlertId).HasColumnName("alert_id");

        builder.HasOne(f => f.Alert)
            .WithMany(a => a.Firings)
            .HasForeignKey(f => f.AlertId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(f => f.FiredAt).HasColumnName("fired_at");
        builder.Property(f => f.ResultCount).HasColumnName("result_count");
    }
}
