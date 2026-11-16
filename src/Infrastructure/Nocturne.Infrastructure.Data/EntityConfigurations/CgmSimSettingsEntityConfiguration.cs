using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.EntityConfigurations;

internal sealed class CgmSimSettingsEntityConfiguration : IEntityTypeConfiguration<CgmSimSettingsEntity>
{
    public void Configure(EntityTypeBuilder<CgmSimSettingsEntity> entity)
    {
        entity.ToTable("cgmsim_settings");
        entity.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasDatabaseName("IX_cgmsim_settings_tenant_id_unique");
    }
}

