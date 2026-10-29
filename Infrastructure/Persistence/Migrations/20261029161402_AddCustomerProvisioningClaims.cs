using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerProvisioningClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProvisioningRequestClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NonceHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    PayloadSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CompanyType = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    ParentCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubsystemsJson = table.Column<string>(type: "varchar(1024)", unicode: false, maxLength: 1024, nullable: false),
                    CustomerReference = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    InstallationId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentTokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    EnrollmentExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnrollmentConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisioningRequestClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProvisioningRequestClaims_Companies_ParentCompanyId",
                        column: x => x.ParentCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvisioningRequestClaims_FiscalPeriods_FiscalPeriodId_CompanyId",
                        columns: x => new { x.FiscalPeriodId, x.CompanyId },
                        principalTable: "FiscalPeriods",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequestClaims_AdminUserId_MustChangePassword",
                table: "ProvisioningRequestClaims",
                columns: new[] { "AdminUserId", "MustChangePassword" });

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequestClaims_CustomerReference_InstallationId",
                table: "ProvisioningRequestClaims",
                columns: new[] { "CustomerReference", "InstallationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequestClaims_EnrollmentTokenHash",
                table: "ProvisioningRequestClaims",
                column: "EnrollmentTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequestClaims_FiscalPeriodId_CompanyId",
                table: "ProvisioningRequestClaims",
                columns: new[] { "FiscalPeriodId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequestClaims_NonceHash",
                table: "ProvisioningRequestClaims",
                column: "NonceHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequestClaims_ParentCompanyId",
                table: "ProvisioningRequestClaims",
                column: "ParentCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequestClaims_RequestId",
                table: "ProvisioningRequestClaims",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProvisioningRequestClaims");
        }
    }
}
