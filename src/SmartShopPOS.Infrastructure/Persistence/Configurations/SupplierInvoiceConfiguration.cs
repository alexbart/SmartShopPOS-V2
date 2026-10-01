using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class SupplierInvoiceConfiguration : IEntityTypeConfiguration<SupplierInvoice>
{
    public void Configure(EntityTypeBuilder<SupplierInvoice> builder)
    {
        builder.ToTable("supplier_invoices", table =>
            table.HasCheckConstraint("ck_supplier_invoices_status_valid", "\"Status\" BETWEEN 1 AND 3"));
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id }).HasName("ak_supplier_invoices_organization_id_id");
        builder.Property(x => x.InternalNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.InvoiceNumber).HasMaxLength(120).IsRequired();
        builder.Property(x => x.NormalizedInvoiceNumber).HasMaxLength(120).IsRequired();
        builder.Property(x => x.InvoiceDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.DueDate).HasColumnType("date");
        builder.Property(x => x.OtherTaxAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.SupplierDocumentNetAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.SupplierDocumentVatAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.SupplierDocumentGrossAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.NetAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.GrossAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).IsRequired(); builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.CreatedByUserId).IsRequired();
        builder.HasIndex(x => new { x.OrganizationId, x.InternalNumber }).IsUnique().HasDatabaseName("ux_supplier_invoices_organization_internal_number");
        builder.HasIndex(x => new { x.OrganizationId, x.SupplierId, x.NormalizedInvoiceNumber }).IsUnique().HasDatabaseName("ux_supplier_invoices_organization_supplier_invoice_number");
        builder.HasIndex(x => new { x.OrganizationId, x.Status, x.InvoiceDate }).HasDatabaseName("ix_supplier_invoices_organization_status_date");
        builder.HasIndex(x => new { x.OrganizationId, x.PurchaseOrderId }).HasDatabaseName("ix_supplier_invoices_organization_order");
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => new { x.OrganizationId, x.SupplierId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => new { x.OrganizationId, x.SupplierId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.SupplierId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.CreatedByUserId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PostedByUserId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupplierInvoiceLineConfiguration : IEntityTypeConfiguration<SupplierInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceLine> builder)
    {
        builder.ToTable("supplier_invoice_lines", table =>
        {
            table.HasCheckConstraint("ck_supplier_invoice_lines_quantity_positive", "\"Quantity\" > 0");
            table.HasCheckConstraint("ck_supplier_invoice_lines_unit_price_nonnegative", "\"UnitPrice\" >= 0");
            table.HasCheckConstraint("ck_supplier_invoice_lines_tax_amount_nonnegative", "\"TaxAmount\" >= 0");
            table.HasCheckConstraint("ck_supplier_invoice_lines_po_reference_pair", "(\"PurchaseOrderId\" IS NULL) = (\"PurchaseOrderLineId\" IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Quantity).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.NetAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Ignore(x => x.GrossAmount);
        builder.Property(x => x.CreatedAt).IsRequired(); builder.Property(x => x.UpdatedAt).IsRequired();
        builder.HasIndex(x => new { x.OrganizationId, x.SupplierInvoiceId }).HasDatabaseName("ix_supplier_invoice_lines_organization_invoice");
        builder.HasIndex(x => new { x.OrganizationId, x.PurchaseOrderId, x.PurchaseOrderLineId }).HasDatabaseName("ix_supplier_invoice_lines_organization_order_line");
        builder.HasOne<SupplierInvoice>().WithMany(x => x.Lines).HasForeignKey(x => new { x.OrganizationId, x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrderLine>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PurchaseOrderId, x.PurchaseOrderLineId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.PurchaseOrderId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxCategory>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.TaxCategoryId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupplierInvoiceNumberSequenceConfiguration : IEntityTypeConfiguration<SupplierInvoiceNumberSequence>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceNumberSequence> builder)
    {
        builder.ToTable("supplier_invoice_number_sequences", table =>
            table.HasCheckConstraint("ck_supplier_invoice_number_sequences_last_number_positive", "\"LastNumber\" > 0"));
        builder.HasKey(x => x.OrganizationId);
        builder.Property(x => x.LastNumber).IsRequired();
        builder.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
