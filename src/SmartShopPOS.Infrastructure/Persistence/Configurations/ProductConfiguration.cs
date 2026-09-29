using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(product => product.Id);
        builder.HasAlternateKey(product => new { product.OrganizationId, product.Id })
            .HasName("ak_products_organization_id_id");
        builder.Property(product => product.Sku).HasMaxLength(64).IsRequired();
        builder.Property(product => product.Barcode).HasMaxLength(128);
        builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
        builder.Property(product => product.Description).HasMaxLength(1000);
        builder.Property(product => product.IsActive).IsRequired();
        builder.Property(product => product.CreatedAt).IsRequired();
        builder.Property(product => product.UpdatedAt).IsRequired();
        builder.HasIndex(product => new { product.OrganizationId, product.Sku })
            .IsUnique()
            .HasDatabaseName("ux_products_organization_sku");
        builder.HasIndex(product => new { product.OrganizationId, product.Barcode })
            .IsUnique()
            .HasFilter("\"Barcode\" IS NOT NULL")
            .HasDatabaseName("ux_products_organization_barcode");
        builder.HasIndex(product => new { product.OrganizationId, product.CategoryId })
            .HasDatabaseName("ix_products_organization_category");
        builder.HasIndex(product => new { product.OrganizationId, product.BrandId })
            .HasDatabaseName("ix_products_organization_brand");
        builder.HasIndex(product => new { product.OrganizationId, product.IsActive })
            .HasDatabaseName("ix_products_organization_active");
        builder.HasOne(product => product.Organization)
            .WithMany()
            .HasForeignKey(product => product.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(product => product.Category)
            .WithMany()
            .HasForeignKey(product => new { product.OrganizationId, product.CategoryId })
            .HasPrincipalKey(category => new { category.OrganizationId, category.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(product => product.Brand)
            .WithMany()
            .HasForeignKey(product => new { product.OrganizationId, product.BrandId })
            .HasPrincipalKey(brand => new { brand.OrganizationId, brand.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(product => product.UnitOfMeasure)
            .WithMany()
            .HasForeignKey(product => new { product.OrganizationId, product.UnitOfMeasureId })
            .HasPrincipalKey(unit => new { unit.OrganizationId, unit.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(product => product.TaxCategory)
            .WithMany()
            .HasForeignKey(product => new { product.OrganizationId, product.TaxCategoryId })
            .HasPrincipalKey(taxCategory => new { taxCategory.OrganizationId, taxCategory.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}