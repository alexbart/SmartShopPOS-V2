using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartShopPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceGoodsReceiptParentIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipt_lines_goods_receipts_OrganizationId_GoodsRece~",
                table: "goods_receipt_lines");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_goods_receipts_organization_order_id",
                table: "goods_receipts",
                columns: new[] { "OrganizationId", "PurchaseOrderId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_OrganizationId_PurchaseOrderId_GoodsRec~",
                table: "goods_receipt_lines",
                columns: new[] { "OrganizationId", "PurchaseOrderId", "GoodsReceiptId" });

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipt_lines_goods_receipts_OrganizationId_PurchaseO~",
                table: "goods_receipt_lines",
                columns: new[] { "OrganizationId", "PurchaseOrderId", "GoodsReceiptId" },
                principalTable: "goods_receipts",
                principalColumns: new[] { "OrganizationId", "PurchaseOrderId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipt_lines_goods_receipts_OrganizationId_PurchaseO~",
                table: "goods_receipt_lines");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_goods_receipts_organization_order_id",
                table: "goods_receipts");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_OrganizationId_PurchaseOrderId_GoodsRec~",
                table: "goods_receipt_lines");

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipt_lines_goods_receipts_OrganizationId_GoodsRece~",
                table: "goods_receipt_lines",
                columns: new[] { "OrganizationId", "GoodsReceiptId" },
                principalTable: "goods_receipts",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
