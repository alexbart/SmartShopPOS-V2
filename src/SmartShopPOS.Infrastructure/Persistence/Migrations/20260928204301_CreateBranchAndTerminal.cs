using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartShopPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateBranchAndTerminal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.Id);
                    table.UniqueConstraint("ak_branches_organization_id_id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_branches_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "terminals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_terminals_branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalTable: "branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[,]
                {
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af07"), null, "branches.view", "View branches" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af08"), null, "branches.create", "Create branches" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af09"), null, "branches.update", "Update branches" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0a"), null, "branches.deactivate", "Deactivate branches" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0b"), null, "terminals.view", "View terminals" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0c"), null, "terminals.create", "Create terminals" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0d"), null, "terminals.update", "Update terminals" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0e"), null, "terminals.deactivate", "Deactivate terminals" }
                });

            migrationBuilder.CreateIndex(
                name: "ux_branches_organization_id_code",
                table: "branches",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_terminals_branch_id",
                table: "terminals",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "ix_terminals_organization_id",
                table: "terminals",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_terminals_OrganizationId_BranchId",
                table: "terminals",
                columns: new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "ux_terminals_branch_id_code",
                table: "terminals",
                columns: new[] { "BranchId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terminals");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af07"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af08"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af09"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0a"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0b"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0c"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0d"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0e"));
        }
    }
}
