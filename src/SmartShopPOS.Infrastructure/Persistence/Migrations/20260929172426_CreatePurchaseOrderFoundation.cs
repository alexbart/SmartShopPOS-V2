using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartShopPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatePurchaseOrderFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_order_number_sequences",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_number_sequences", x => x.OrganizationId);
                    table.CheckConstraint("ck_purchase_order_number_sequences_last_number_positive", "\"LastNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_purchase_order_number_sequences_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OrderDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpectedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Id);
                    table.UniqueConstraint("ak_purchase_orders_organization_id_id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_purchase_orders_status_valid", "\"Status\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_purchase_orders_branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalTable: "branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_suppliers_OrganizationId_SupplierId",
                        columns: x => new { x.OrganizationId, x.SupplierId },
                        principalTable: "suppliers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_users_OrganizationId_CreatedByUserId",
                        columns: x => new { x.OrganizationId, x.CreatedByUserId },
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_users_OrganizationId_UpdatedByUserId",
                        columns: x => new { x.OrganizationId, x.UpdatedByUserId },
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[,]
                {
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af31"), null, "purchase_orders.view", "View purchase orders" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af32"), null, "purchase_orders.create", "Create purchase orders" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af33"), null, "purchase_orders.update", "Update draft purchase orders" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af34"), null, "purchase_orders.submit", "Submit purchase orders" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af35"), null, "purchase_orders.cancel", "Cancel purchase orders" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_organization_branch",
                table: "purchase_orders",
                columns: new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_organization_order_date",
                table: "purchase_orders",
                columns: new[] { "OrganizationId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_organization_status",
                table: "purchase_orders",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_organization_supplier",
                table: "purchase_orders",
                columns: new[] { "OrganizationId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_OrganizationId_CreatedByUserId",
                table: "purchase_orders",
                columns: new[] { "OrganizationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_OrganizationId_UpdatedByUserId",
                table: "purchase_orders",
                columns: new[] { "OrganizationId", "UpdatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "ux_purchase_orders_organization_order_number",
                table: "purchase_orders",
                columns: new[] { "OrganizationId", "OrderNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_order_number_sequences");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af31"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af32"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af33"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af34"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af35"));
        }
    }
}
