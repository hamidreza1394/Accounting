using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyMembershipRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyMembershipRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyMembershipRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyMembershipRequests_Companies_DestinationTenantId",
                        column: x => x.DestinationTenantId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyMembershipRequests_CompanyRoles_ApprovedRoleId_DestinationTenantId",
                        columns: x => new { x.ApprovedRoleId, x.DestinationTenantId },
                        principalTable: "CompanyRoles",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMembershipRequests_ApprovedRoleId_DestinationTenantId",
                table: "CompanyMembershipRequests",
                columns: new[] { "ApprovedRoleId", "DestinationTenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMembershipRequests_DestinationTenantId_Status_RequestedAtUtc",
                table: "CompanyMembershipRequests",
                columns: new[] { "DestinationTenantId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMembershipRequests_UserId_DestinationTenantId",
                table: "CompanyMembershipRequests",
                columns: new[] { "UserId", "DestinationTenantId" },
                unique: true,
                filter: "[Status] = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyMembershipRequests");
        }
    }
}
