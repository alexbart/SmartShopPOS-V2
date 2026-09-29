using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("units_of_measure");
        builder.HasKey(unit => unit.Id);
        builder.HasAlternateKey(unit => new { unit.OrganizationId, unit.Id })
            .HasName("ak_units_of_measure_organization_id_id");
        builder.Property(unit => unit.Code).HasMaxLength(32).IsRequired();
        builder.Property(unit => unit.Name).HasMaxLength(120).IsRequired();
        builder.Property(unit => unit.Symbol).HasMaxLength(16);
        builder.Property(unit => unit.IsActive).IsRequired();
        builder.Property(unit => unit.CreatedAt).IsRequired();
        builder.Property(unit => unit.UpdatedAt).IsRequired();
        builder.HasIndex(unit => new { unit.OrganizationId, unit.Code })
            .IsUnique()
            .HasDatabaseName("ux_units_of_measure_organization_code");
        builder.HasIndex(unit => new { unit.OrganizationId, unit.IsActive })
            .HasDatabaseName("ix_units_of_measure_organization_active");
        builder.HasOne(unit => unit.Organization)
            .WithMany()
            .HasForeignKey(unit => unit.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}