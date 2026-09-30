using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("purchase_order_lines", table =>
        {
            table.HasCheckConstraint("ck_purchase_order_lines_quantity_positive", "\"Quantity\" > 0");
            table.HasCheckConstraint("ck_purchase_order_lines_unit_cost_non_negative", "\"UnitCost\" >= 0");
        });
        builder.HasKey(line => line.Id);
        builder.HasAlternateKey(line => new { line.OrganizationId, line.PurchaseOrderId, line.Id })
            .HasName("ak_purchase_order_lines_organization_order_id");
        builder.Property(line => line.Quantity).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(line => line.UnitCost).HasColumnType("numeric(18,4)").IsRequired();
        builder.Ignore(line => line.LineTotal);
        builder.Property(line => line.Notes).HasMaxLength(1000);
        builder.Property(line => line.CreatedAt).IsRequired();
        builder.Property(line => line.UpdatedAt).IsRequired();

        builder.HasIndex(line => new { line.OrganizationId, line.PurchaseOrderId, line.ProductId })
            .IsUnique()
            .HasDatabaseName("ux_purchase_order_lines_organization_order_product");
        builder.HasIndex(line => new { line.OrganizationId, line.ProductId })
            .HasDatabaseName("ix_purchase_order_lines_organization_product");

        builder.HasOne(line => line.PurchaseOrder)
            .WithMany()
            .HasForeignKey(line => new { line.OrganizationId, line.PurchaseOrderId })
            .HasPrincipalKey(order => new { order.OrganizationId, order.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(line => line.Product)
            .WithMany()
            .HasForeignKey(line => new { line.OrganizationId, line.ProductId })
            .HasPrincipalKey(product => new { product.OrganizationId, product.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
