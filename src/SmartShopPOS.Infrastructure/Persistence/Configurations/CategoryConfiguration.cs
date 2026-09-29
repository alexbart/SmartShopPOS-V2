using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(category => category.Id);
        builder.HasAlternateKey(category => new { category.OrganizationId, category.Id })
            .HasName("ak_categories_organization_id_id");
        builder.Property(category => category.Name).HasMaxLength(120).IsRequired();
        builder.Property(category => category.NormalizedName).HasMaxLength(120).IsRequired();
        builder.Property(category => category.Description).HasMaxLength(1000);
        builder.Property(category => category.IsActive).IsRequired();
        builder.Property(category => category.CreatedAt).IsRequired();
        builder.Property(category => category.UpdatedAt).IsRequired();
        builder.HasIndex(category => new { category.OrganizationId, category.NormalizedName })
            .IsUnique()
            .HasDatabaseName("ux_categories_organization_normalized_name");
        builder.HasOne(category => category.Organization)
            .WithMany()
            .HasForeignKey(category => category.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}