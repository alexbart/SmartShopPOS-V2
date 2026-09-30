using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartShopPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateGoodsReceiptFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_purchase_order_lines_organization_order_id",
                table: "purchase_order_lines",
                columns: new[] { "OrganizationId", "PurchaseOrderId", "Id" });

            migrationBuilder.CreateTable(
                name: "goods_receipt_idempotency",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_idempotency", x => new { x.OrganizationId, x.Key });
                    table.ForeignKey(
                        name: "FK_goods_receipt_idempotency_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipts", x => x.Id);
                    table.UniqueConstraint("ak_goods_receipts_organization_id_id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_goods_receipts_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_purchase_orders_OrganizationId_PurchaseOrder~",
                        columns: x => new { x.OrganizationId, x.PurchaseOrderId },
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_users_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_lines", x => x.Id);
                    table.CheckConstraint("ck_goods_receipt_lines_quantity_positive", "\"QuantityReceived\" > 0");
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_goods_receipts_OrganizationId_GoodsRece~",
                        columns: x => new { x.OrganizationId, x.GoodsReceiptId },
                        principalTable: "goods_receipts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_purchase_order_lines_OrganizationId_Pur~",
                        columns: x => new { x.OrganizationId, x.PurchaseOrderId, x.PurchaseOrderLineId },
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "OrganizationId", "PurchaseOrderId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[,]
                {
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3a"), null, "goods_receipts.view", "View goods receipts" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3b"), null, "goods_receipts.create", "Create goods receipts" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_idempotency_GoodsReceiptId",
                table: "goods_receipt_idempotency",
                column: "GoodsReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_lines_organization_order_line",
                table: "goods_receipt_lines",
                columns: new[] { "OrganizationId", "PurchaseOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_OrganizationId_PurchaseOrderId_Purchase~",
                table: "goods_receipt_lines",
                columns: new[] { "OrganizationId", "PurchaseOrderId", "PurchaseOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "ux_goods_receipt_lines_receipt_order_line",
                table: "goods_receipt_lines",
                columns: new[] { "OrganizationId", "GoodsReceiptId", "PurchaseOrderLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipts_organization_order",
                table: "goods_receipts",
                columns: new[] { "OrganizationId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipts_organization_received_at",
                table: "goods_receipts",
                columns: new[] { "OrganizationId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_OrganizationId_CreatedByUserId",
                table: "goods_receipts",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "ux_goods_receipts_organization_number",
                table: "goods_receipts",
                columns: new[] { "OrganizationId", "ReceiptNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "goods_receipt_idempotency");

            migrationBuilder.DropTable(
                name: "goods_receipt_lines");

            migrationBuilder.DropTable(
                name: "goods_receipts");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_purchase_order_lines_organization_order_id",
                table: "purchase_order_lines");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3a"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af3b"));
        }
    }
}
