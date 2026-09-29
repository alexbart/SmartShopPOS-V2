using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartShopPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateUserBranchAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SelectedBranchId",
                table: "authentication_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeactivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_branches_branches_OrganizationId_BranchId",
                        columns: x => new { x.OrganizationId, x.BranchId },
                        principalTable: "branches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_branches_users_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalTable: "users",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Description", "Key", "Name" },
                values: new object[,]
                {
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0f"), null, "user_branch_assignments.view", "View user branch assignments" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af10"), null, "user_branch_assignments.create", "Assign users to branches" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af11"), null, "user_branch_assignments.deactivate", "Deactivate user branch assignments" },
                    { new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af12"), null, "branch_context.select", "Select operational branch context" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_authentication_sessions_OrganizationId_SelectedBranchId",
                table: "authentication_sessions",
                columns: new[] { "OrganizationId", "SelectedBranchId" });

            migrationBuilder.CreateIndex(
                name: "ix_authentication_sessions_selected_branch_id",
                table: "authentication_sessions",
                column: "SelectedBranchId");

            migrationBuilder.CreateIndex(
                name: "ix_user_branches_organization_branch",
                table: "user_branches",
                columns: new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "ix_user_branches_organization_user",
                table: "user_branches",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "ux_user_branches_active_assignment",
                table: "user_branches",
                columns: new[] { "OrganizationId", "UserId", "BranchId" },
                unique: true,
                filter: "\"DeactivatedAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_authentication_sessions_branches_OrganizationId_SelectedBra~",
                table: "authentication_sessions",
                columns: new[] { "OrganizationId", "SelectedBranchId" },
                principalTable: "branches",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_authentication_sessions_branches_OrganizationId_SelectedBra~",
                table: "authentication_sessions");

            migrationBuilder.DropTable(
                name: "user_branches");

            migrationBuilder.DropIndex(
                name: "IX_authentication_sessions_OrganizationId_SelectedBranchId",
                table: "authentication_sessions");

            migrationBuilder.DropIndex(
                name: "ix_authentication_sessions_selected_branch_id",
                table: "authentication_sessions");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0f"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af10"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af11"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af12"));

            migrationBuilder.DropColumn(
                name: "SelectedBranchId",
                table: "authentication_sessions");
        }
    }
}
