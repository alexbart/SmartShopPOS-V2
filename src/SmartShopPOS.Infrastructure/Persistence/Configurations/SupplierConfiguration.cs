using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");
        builder.HasKey(supplier => supplier.Id);
        builder.HasAlternateKey(supplier => new { supplier.OrganizationId, supplier.Id })
            .HasName("ak_suppliers_organization_id_id");
        builder.Property(supplier => supplier.Code).HasMaxLength(64).IsRequired();
        builder.Property(supplier => supplier.Name).HasMaxLength(200).IsRequired();
        builder.Property(supplier => supplier.Description).HasMaxLength(1000);
        builder.Property(supplier => supplier.ContactPerson).HasMaxLength(160);
        builder.Property(supplier => supplier.Phone).HasMaxLength(64);
        builder.Property(supplier => supplier.Email).HasMaxLength(320);
        builder.Property(supplier => supplier.Address).HasMaxLength(1000);
        builder.Property(supplier => supplier.TaxIdentifier).HasMaxLength(64);
        builder.Property(supplier => supplier.BusinessRegistrationNumber).HasMaxLength(64);
        builder.Property(supplier => supplier.IsActive).IsRequired();
        builder.Property(supplier => supplier.CreatedAt).IsRequired();
        builder.Property(supplier => supplier.UpdatedAt).IsRequired();
        builder.HasIndex(supplier => new { supplier.OrganizationId, supplier.Code })
            .IsUnique()
            .HasDatabaseName("ux_suppliers_organization_code");
        builder.HasIndex(supplier => new { supplier.OrganizationId, supplier.IsActive })
            .HasDatabaseName("ix_suppliers_organization_active");
        builder.HasOne(supplier => supplier.Organization)
            .WithMany()
            .HasForeignKey(supplier => supplier.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
