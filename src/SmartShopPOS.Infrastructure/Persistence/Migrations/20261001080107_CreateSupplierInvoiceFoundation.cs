using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartShopPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateSupplierInvoiceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_purchase_orders_organization_supplier_id",
                table: "purchase_orders",
                columns: new[] { "OrganizationId", "SupplierId", "Id" });

            migrationBuilder.CreateTable(
                name: "supplier_invoice_number_sequences",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_invoice_number_sequences", x => x.OrganizationId);
                    table.CheckConstraint("ck_supplier_invoice_number_sequences_last_number_positive", "\"LastNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_supplier_invoice_number_sequences_organizations_Organizatio~",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    InternalNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedInvoiceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OtherTaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SupplierDocumentNetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SupplierDocumentVatAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SupplierDocumentGrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_invoices", x => x.Id);
                    table.UniqueConstraint("ak_supplier_invoices_organization_id_id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_supplier_invoices_status_valid", "\"Status\" BETWEEN 1 AND 3");
                    table.CheckConstraint("ck_supplier_invoices_amounts_nonnegative", "\"OtherTaxAmount\" >= 0 AND \"SupplierDocumentNetAmount\" >= 0 AND \"SupplierDocumentVatAmount\" >= 0 AND \"SupplierDocumentGrossAmount\" >= 0 AND \"NetAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"GrossAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_supplier_invoices_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_invoices_purchase_orders_OrganizationId_SupplierId~",
                        columns: x => new { x.OrganizationId, x.SupplierId, x.PurchaseOrderId },
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "OrganizationId", "SupplierId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_invoices_suppliers_OrganizationId_SupplierId",
                        columns: x => new { x.OrganizationId, x.SupplierId },
                        principalTable: "suppliers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_invoices_users_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_invoices_users_OrganizationId_PostedByUserId",
                        columns: x => new { x.OrganizationId, x.PostedByUserId },
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_invoice_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaxCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_invoice_lines", x => x.Id);
                    table.CheckConstraint("ck_supplier_invoice_lines_po_reference_pair", "(\"PurchaseOrderId\" IS NULL) = (\"PurchaseOrderLineId\" IS NULL)");
                    table.CheckConstraint("ck_supplier_invoice_lines_quantity_positive", "\"Quantity\" > 0");
                    table.CheckConstraint("ck_supplier_invoice_lines_tax_amount_nonnegative", "\"TaxAmount\" >= 0");
                    table.CheckConstraint("ck_supplier_invoice_lines_unit_price_nonnegative", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_supplier_invoice_lines_purchase_order_lines_OrganizationId_~",
                        columns: x => new { x.OrganizationId, x.PurchaseOrderId, x.PurchaseOrderLineId },
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "OrganizationId", "PurchaseOrderId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_invoice_lines_supplier_invoices_OrganizationId_Sup~",
                        columns: x => new { x.OrganizationId, x.SupplierInvoiceId },
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_invoice_lines_tax_categories_OrganizationId_TaxCat~",
                        columns: x => new { x.OrganizationId, x.TaxCategoryId },
                        principalTable: "tax_categories",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[,]
                {
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3c"), null, "supplier_invoices.view", "View supplier invoices" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3d"), null, "supplier_invoices.create", "Create supplier invoices" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3e"), null, "supplier_invoices.update", "Update draft supplier invoices" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3f"), null, "supplier_invoices.post", "Post supplier invoices" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af40"), null, "supplier_invoices.cancel", "Cancel draft supplier invoices" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoice_lines_organization_invoice",
                table: "supplier_invoice_lines",
                columns: new[] { "OrganizationId", "SupplierInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoice_lines_organization_order_line",
                table: "supplier_invoice_lines",
                columns: new[] { "OrganizationId", "PurchaseOrderId", "PurchaseOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoice_lines_OrganizationId_TaxCategoryId",
                table: "supplier_invoice_lines",
                columns: new[] { "OrganizationId", "TaxCategoryId" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoices_organization_order",
                table: "supplier_invoices",
                columns: new[] { "OrganizationId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoices_organization_status_date",
                table: "supplier_invoices",
                columns: new[] { "OrganizationId", "Status", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_OrganizationId_CreatedByUserId",
                table: "supplier_invoices",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_OrganizationId_PostedByUserId",
                table: "supplier_invoices",
                columns: new[] { "OrganizationId", "PostedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_OrganizationId_SupplierId_PurchaseOrderId",
                table: "supplier_invoices",
                columns: new[] { "OrganizationId", "SupplierId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "ux_supplier_invoices_organization_internal_number",
                table: "supplier_invoices",
                columns: new[] { "OrganizationId", "InternalNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_supplier_invoices_organization_supplier_invoice_number",
                table: "supplier_invoices",
                columns: new[] { "OrganizationId", "SupplierId", "NormalizedInvoiceNumber" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION enforce_supplier_invoice_line_purchase_order_match() RETURNS trigger AS $$
                DECLARE invoice_purchase_order_id uuid;
                BEGIN
                    IF NEW.PurchaseOrderLineId IS NOT NULL THEN
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
                ON supplier_invoice_lines
                FOR EACH ROW EXECUTE FUNCTION enforce_supplier_invoice_line_purchase_order_match();
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
            migrationBuilder.DropTable(
                name: "supplier_invoice_lines");

            migrationBuilder.DropTable(
                name: "supplier_invoice_number_sequences");

            migrationBuilder.DropTable(
                name: "supplier_invoices");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_purchase_orders_organization_supplier_id",
                table: "purchase_orders");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3c"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3d"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3e"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3f"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af40"));
        }
    }
}
