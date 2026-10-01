using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders", table =>
            table.HasCheckConstraint("ck_purchase_orders_status_valid", "\"Status\" BETWEEN 1 AND 3"));
        builder.HasKey(order => order.Id);
        builder.HasAlternateKey(order => new { order.OrganizationId, order.Id })
            .HasName("ak_purchase_orders_organization_id_id");
        builder.HasAlternateKey(order => new { order.OrganizationId, order.SupplierId, order.Id })
            .HasName("ak_purchase_orders_organization_supplier_id");
        builder.Property(order => order.OrderNumber).HasMaxLength(32).IsRequired();
        builder.Property(order => order.Status).HasConversion<int>().IsRequired();
        builder.Property(order => order.OrderDate).IsRequired();
        builder.Property(order => order.ExpectedDate);
        builder.Property(order => order.Notes).HasMaxLength(1000);
        builder.Property(order => order.CreatedAt).IsRequired();
        builder.Property(order => order.UpdatedAt).IsRequired();
        builder.Property(order => order.CreatedByUserId).IsRequired();

        builder.HasIndex(order => new { order.OrganizationId, order.OrderNumber })
            .IsUnique()
            .HasDatabaseName("ux_purchase_orders_organization_order_number");
        builder.HasIndex(order => new { order.OrganizationId, order.Status })
            .HasDatabaseName("ix_purchase_orders_organization_status");
        builder.HasIndex(order => new { order.OrganizationId, order.SupplierId })
            .HasDatabaseName("ix_purchase_orders_organization_supplier");
        builder.HasIndex(order => new { order.OrganizationId, order.BranchId })
            .HasDatabaseName("ix_purchase_orders_organization_branch");
        builder.HasIndex(order => new { order.OrganizationId, order.OrderDate })
            .HasDatabaseName("ix_purchase_orders_organization_order_date");

        builder.HasOne(order => order.Organization)
            .WithMany()
            .HasForeignKey(order => order.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.Supplier)
            .WithMany()
            .HasForeignKey(order => new { order.OrganizationId, order.SupplierId })
            .HasPrincipalKey(supplier => new { supplier.OrganizationId, supplier.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.Branch)
            .WithMany()
            .HasForeignKey(order => new { order.OrganizationId, order.BranchId })
            .HasPrincipalKey(branch => new { branch.OrganizationId, branch.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.CreatedByUser)
            .WithMany()
            .HasForeignKey(order => new { order.OrganizationId, order.CreatedByUserId })
            .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.UpdatedByUser)
            .WithMany()
            .HasForeignKey(order => new { order.OrganizationId, order.UpdatedByUserId })
            .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
