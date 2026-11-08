using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaasDemo.Domain.Tenants;

namespace SaasDemo.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Slug).HasMaxLength(TenantSlug.MaxLength).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(t => t.Slug).IsUnique();
        builder.HasIndex(t => t.EntraTenantId).IsUnique();
    }
}
