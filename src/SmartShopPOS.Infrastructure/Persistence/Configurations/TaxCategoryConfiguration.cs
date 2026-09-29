using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class TaxCategoryConfiguration : IEntityTypeConfiguration<TaxCategory>
{
    public void Configure(EntityTypeBuilder<TaxCategory> builder)
    {
        builder.ToTable("tax_categories", table => table.HasCheckConstraint(
            "ck_tax_categories_rate_percentage",
            "\"Rate\" >= 0 AND \"Rate\" <= 100"));
        builder.HasKey(taxCategory => taxCategory.Id);
        builder.HasAlternateKey(taxCategory => new { taxCategory.OrganizationId, taxCategory.Id })
            .HasName("ak_tax_categories_organization_id_id");
        builder.Property(taxCategory => taxCategory.Code).HasMaxLength(32).IsRequired();
        builder.Property(taxCategory => taxCategory.Name).HasMaxLength(120).IsRequired();
        builder.Property(taxCategory => taxCategory.Rate).HasPrecision(6, 3).IsRequired();
        builder.Property(taxCategory => taxCategory.IsActive).IsRequired();
        builder.Property(taxCategory => taxCategory.CreatedAt).IsRequired();
        builder.Property(taxCategory => taxCategory.UpdatedAt).IsRequired();
        builder.HasIndex(taxCategory => new { taxCategory.OrganizationId, taxCategory.Code })
            .IsUnique()
            .HasDatabaseName("ux_tax_categories_organization_code");
        builder.HasIndex(taxCategory => new { taxCategory.OrganizationId, taxCategory.IsActive })
            .HasDatabaseName("ix_tax_categories_organization_active");
        builder.HasOne(taxCategory => taxCategory.Organization)
            .WithMany()
            .HasForeignKey(taxCategory => taxCategory.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}