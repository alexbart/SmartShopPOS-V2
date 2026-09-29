using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands");
        builder.HasKey(brand => brand.Id);
        builder.HasAlternateKey(brand => new { brand.OrganizationId, brand.Id })
            .HasName("ak_brands_organization_id_id");
        builder.Property(brand => brand.Name).HasMaxLength(120).IsRequired();
        builder.Property(brand => brand.NormalizedName).HasMaxLength(120).IsRequired();
        builder.Property(brand => brand.Description).HasMaxLength(1000);
        builder.Property(brand => brand.IsActive).IsRequired();
        builder.Property(brand => brand.CreatedAt).IsRequired();
        builder.Property(brand => brand.UpdatedAt).IsRequired();
        builder.HasIndex(brand => new { brand.OrganizationId, brand.NormalizedName })
            .IsUnique()
            .HasDatabaseName("ux_brands_organization_normalized_name");
        builder.HasOne(brand => brand.Organization)
            .WithMany()
            .HasForeignKey(brand => brand.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}