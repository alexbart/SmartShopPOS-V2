using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("goods_receipts");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id }).HasName("ak_goods_receipts_organization_id_id");
        builder.Property(x => x.ReceiptNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ReceivedAt).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedByUserId).IsRequired();
        builder.HasIndex(x => new { x.OrganizationId, x.ReceiptNumber }).IsUnique().HasDatabaseName("ux_goods_receipts_organization_number");
        builder.HasIndex(x => new { x.OrganizationId, x.PurchaseOrderId }).HasDatabaseName("ix_goods_receipts_organization_order");
        builder.HasIndex(x => new { x.OrganizationId, x.ReceivedAt }).HasDatabaseName("ix_goods_receipts_organization_received_at");
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.CreatedByUserId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.ToTable("goods_receipt_lines", table => table.HasCheckConstraint("ck_goods_receipt_lines_quantity_positive", "\"QuantityReceived\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuantityReceived).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => new { x.OrganizationId, x.GoodsReceiptId, x.PurchaseOrderLineId }).IsUnique().HasDatabaseName("ux_goods_receipt_lines_receipt_order_line");
        builder.HasIndex(x => new { x.OrganizationId, x.PurchaseOrderLineId }).HasDatabaseName("ix_goods_receipt_lines_organization_order_line");
        builder.HasOne<GoodsReceipt>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.GoodsReceiptId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrderLine>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseOrderId, x.PurchaseOrderLineId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.PurchaseOrderId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GoodsReceiptIdempotencyConfiguration : IEntityTypeConfiguration<GoodsReceiptIdempotency>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptIdempotency> builder)
    {
        builder.ToTable("goods_receipt_idempotency");
        builder.HasKey(x => new { x.OrganizationId, x.Key });
        builder.Property(x => x.Key).HasMaxLength(128);
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.GoodsReceiptId).IsUnique();
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
