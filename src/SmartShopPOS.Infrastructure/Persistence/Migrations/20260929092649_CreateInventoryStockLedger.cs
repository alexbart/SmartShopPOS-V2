using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartShopPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateInventoryStockLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Key", "Name" },
                values: new object[,]
                {
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af29"), "inventory.view", "View inventory balances" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af2a"), "inventory.opening_balance", "Create inventory opening balances" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af2b"), "inventory.adjust", "Adjust inventory" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af2c"), "inventory.movements.view", "View stock movements" }
                });

            migrationBuilder.CreateTable(
                name: "inventory_balances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_balances", x => x.Id);
                    table.UniqueConstraint("ak_inventory_balances_organization_id_id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_inventory_balances_quantity_non_negative", "\"QuantityOnHand\" >= 0");
                    table.ForeignKey(
                        name: "FK_inventory_balances_branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalTable: "branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_balances_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_balances_products_OrganizationId_ProductId",
                        columns: x => new { x.OrganizationId, x.ProductId },
                        principalTable: "products",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.Id);
                    table.UniqueConstraint("ak_stock_movements_organization_id_id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_stock_movements_quantity_positive", "\"Quantity\" > 0");
                    table.CheckConstraint("ck_stock_movements_type_valid", "\"MovementType\" BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_stock_movements_branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalTable: "branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_products_OrganizationId_ProductId",
                        columns: x => new { x.OrganizationId, x.ProductId },
                        principalTable: "products",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_balances_organization_branch",
                table: "inventory_balances",
                columns: new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_balances_organization_product",
                table: "inventory_balances",
                columns: new[] { "OrganizationId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "ux_inventory_balances_organization_branch_product",
                table: "inventory_balances",
                columns: new[] { "OrganizationId", "BranchId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_organization_branch_occurred",
                table: "stock_movements",
                columns: new[] { "OrganizationId", "BranchId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_organization_branch_product_occurred",
                table: "stock_movements",
                columns: new[] { "OrganizationId", "BranchId", "ProductId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_organization_product_occurred",
                table: "stock_movements",
                columns: new[] { "OrganizationId", "ProductId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_balances");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValues: new object[]
                {
                    new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af29"),
                    new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af2a"),
                    new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af2b"),
                    new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af2c")
                });
        }
    }
}
