using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.ToTable("product_prices");
        builder.HasKey(price => price.Id);
        builder.Property(price => price.CostPrice).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(price => price.SellingPrice).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(price => price.EffectiveFrom).IsRequired();
        builder.Property(price => price.EffectiveTo);
        builder.Property(price => price.CreatedAt).IsRequired();
        builder.Property(price => price.UpdatedAt).IsRequired();
        builder.HasIndex(price => new { price.OrganizationId, price.ProductId, price.EffectiveFrom })
            .HasDatabaseName("ix_product_prices_organization_product_effective_from");
        builder.HasIndex(price => new { price.OrganizationId, price.ProductId, price.EffectiveTo })
            .HasDatabaseName("ix_product_prices_organization_product_effective_to");
        builder.HasIndex(price => new { price.OrganizationId, price.ProductId })
            .IsUnique()
            .HasFilter("\"EffectiveTo\" IS NULL")
            .HasDatabaseName("ux_product_prices_organization_product_active");
        builder.HasOne(price => price.Organization)
            .WithMany()
            .HasForeignKey(price => price.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(price => price.Product)
            .WithMany()
            .HasForeignKey(price => new { price.OrganizationId, price.ProductId })
            .HasPrincipalKey(product => new { product.OrganizationId, product.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
