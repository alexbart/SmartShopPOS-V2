using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartShopPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateGoodsReceiptNumberSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "goods_receipt_number_sequences",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_number_sequences", x => x.OrganizationId);
                    table.CheckConstraint("ck_goods_receipt_number_sequences_last_number_positive", "\"LastNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_goods_receipt_number_sequences_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "goods_receipt_number_sequences");
        }
    }
}
