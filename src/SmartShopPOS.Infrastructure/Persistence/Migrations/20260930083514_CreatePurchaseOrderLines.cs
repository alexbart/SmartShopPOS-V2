using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartShopPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatePurchaseOrderLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_lines", x => x.Id);
                    table.CheckConstraint("ck_purchase_order_lines_quantity_positive", "\"Quantity\" > 0");
                    table.CheckConstraint("ck_purchase_order_lines_unit_cost_non_negative", "\"UnitCost\" >= 0");
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_products_OrganizationId_ProductId",
                        columns: x => new { x.OrganizationId, x.ProductId },
                        principalTable: "products",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_purchase_orders_OrganizationId_Purchas~",
                        columns: x => new { x.OrganizationId, x.PurchaseOrderId },
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[,]
                {
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af36"), null, "purchase_orders.lines.view", "View purchase order lines" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af37"), null, "purchase_orders.lines.create", "Add purchase order lines" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af38"), null, "purchase_orders.lines.update", "Update purchase order lines" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af39"), null, "purchase_orders.lines.delete", "Delete purchase order lines" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_organization_product",
                table: "purchase_order_lines",
                columns: new[] { "OrganizationId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "ux_purchase_order_lines_organization_order_product",
                table: "purchase_order_lines",
                columns: new[] { "OrganizationId", "PurchaseOrderId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_order_lines");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af36"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af37"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af38"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af39"));
        }
    }
}
