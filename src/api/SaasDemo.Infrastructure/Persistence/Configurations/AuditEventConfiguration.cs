using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaasDemo.Domain.Audit;
using SaasDemo.Domain.Tenants;

namespace SaasDemo.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        // dbo.AuditEvents is covered by the RLS security policy (migration AddAuditEventsAndRls); keep the names in sync.
        builder.ToTable("AuditEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.ActorDisplayName).HasMaxLength(AuditEvent.ActorDisplayNameMaxLength).IsRequired();
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Details).HasMaxLength(AuditEvent.DetailsMaxLength);

        // The log is read newest first, per tenant.
        builder.HasIndex(e => new { e.TenantId, e.OccurredAtUtc }).IsDescending(false, true);
        // "This user's recent sign-ins" (/me/activity and the 30-minute sign-in throttle).
        builder.HasIndex(e => new { e.TenantId, e.TargetUserId, e.OccurredAtUtc });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
