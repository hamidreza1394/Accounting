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
            migrationBuilder.Sql("ALTER TABLE [CompanyMembershipRequests] ADD CONSTRAINT [CK_CompanyMembershipRequests_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected'));");
            migrationBuilder.Sql("ALTER TABLE [CompanyMembershipRequests] ADD CONSTRAINT [CK_CompanyMembershipRequests_DecisionState] CHECK (([Status] = 'Pending' AND [DecidedByUserId] IS NULL AND [DecidedAtUtc] IS NULL AND [ApprovedRoleId] IS NULL) OR ([Status] = 'Approved' AND [DecidedByUserId] IS NOT NULL AND [DecidedByUserId] <> [UserId] AND [DecidedAtUtc] IS NOT NULL AND [ApprovedRoleId] IS NOT NULL) OR ([Status] = 'Rejected' AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL AND [ApprovedRoleId] IS NULL));");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'users.membership.approve') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1103-4d00-8000-000000000001', 'users.membership.approve', N'تأیید درخواست عضویت در شرکت', N'اجازه مشاهده و تعیین تکلیف درخواست عضویت در شرکت مقصد.', 21000, SYSDATETIME());");

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [CompanyMembershipRequests]) THROW 51000, 'Rollback blocked: export or retain membership request history before dropping CompanyMembershipRequests.', 1;");
            migrationBuilder.Sql("DELETE FROM [PermissionDefinitions] WHERE [Id] = 'f47a2026-1103-4d00-8000-000000000001' AND NOT EXISTS (SELECT 1 FROM [CompanyRolePermissions] WHERE [CompanyRolePermissions].[PermissionId] = [PermissionDefinitions].[Id]);");

            migrationBuilder.DropTable(
                name: "CompanyMembershipRequests");
        }
    }
}
