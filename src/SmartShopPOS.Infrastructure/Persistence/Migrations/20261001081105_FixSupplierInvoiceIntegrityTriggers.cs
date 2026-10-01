using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartShopPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixSupplierInvoiceIntegrityTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_supplier_invoice_purchase_order_match ON supplier_invoices; DROP FUNCTION IF EXISTS prevent_supplier_invoice_order_mismatch(); DROP TRIGGER IF EXISTS tr_supplier_invoice_line_purchase_order_match ON supplier_invoice_lines; DROP FUNCTION IF EXISTS enforce_supplier_invoice_line_purchase_order_match();");
            migrationBuilder.Sql("""
                CREATE FUNCTION enforce_supplier_invoice_line_purchase_order_match() RETURNS trigger AS $$
                DECLARE invoice_purchase_order_id uuid;
                BEGIN
                    IF NEW."PurchaseOrderLineId" IS NOT NULL THEN
                        SELECT "PurchaseOrderId" INTO invoice_purchase_order_id
                        FROM supplier_invoices
                        WHERE "OrganizationId" = NEW."OrganizationId" AND "Id" = NEW."SupplierInvoiceId";
                        IF invoice_purchase_order_id IS NULL OR invoice_purchase_order_id <> NEW."PurchaseOrderId" THEN
                            RAISE EXCEPTION 'supplier invoice line purchase order must match its invoice';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER tr_supplier_invoice_line_purchase_order_match
                BEFORE INSERT OR UPDATE OF "OrganizationId", "SupplierInvoiceId", "PurchaseOrderId", "PurchaseOrderLineId"
                ON supplier_invoice_lines FOR EACH ROW EXECUTE FUNCTION enforce_supplier_invoice_line_purchase_order_match();
                CREATE FUNCTION prevent_supplier_invoice_order_mismatch() RETURNS trigger AS $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM supplier_invoice_lines l
                        WHERE l."OrganizationId" = NEW."OrganizationId" AND l."SupplierInvoiceId" = NEW."Id"
                          AND l."PurchaseOrderLineId" IS NOT NULL AND l."PurchaseOrderId" IS DISTINCT FROM NEW."PurchaseOrderId") THEN
                        RAISE EXCEPTION 'supplier invoice purchase order cannot change while linked lines exist';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER tr_supplier_invoice_purchase_order_match
                BEFORE UPDATE OF "PurchaseOrderId" ON supplier_invoices
                FOR EACH ROW EXECUTE FUNCTION prevent_supplier_invoice_order_mismatch();
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_supplier_invoice_purchase_order_match ON supplier_invoices; DROP FUNCTION IF EXISTS prevent_supplier_invoice_order_mismatch(); DROP TRIGGER IF EXISTS tr_supplier_invoice_line_purchase_order_match ON supplier_invoice_lines; DROP FUNCTION IF EXISTS enforce_supplier_invoice_line_purchase_order_match();");

        }
    }
}
