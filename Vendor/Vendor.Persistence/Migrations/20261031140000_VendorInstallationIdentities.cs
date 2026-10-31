using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faranou.Vendor.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VendorInstallationIdentities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorInstallationIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    KeyVersion = table.Column<int>(type: "int", nullable: false),
                    ProviderName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    AssuranceLevel = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    OsInstallationMarkerDigest = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    PublicKeyFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    PublicKeySubjectPublicKeyInfoBase64 = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: false),
                    SoftwareProviderRiskAcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SoftwareProviderRiskAcceptedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SoftwareProviderConsentReceiptJson = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    State = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    RegisteredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RetiredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorInstallationIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorInstallationIdentities_VendorInstallations_InstallationId",
                        column: x => x.InstallationId,
                        principalTable: "VendorInstallations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.CheckConstraint("CK_VendorInstallationIdentities_AssuranceProvider",
                        "([AssuranceLevel] = 'HardwareTpm' AND [ProviderName] = 'Microsoft Platform Crypto Provider') OR ([AssuranceLevel] = 'SoftwareProtected' AND [ProviderName] = 'Microsoft Software Key Storage Provider')");
                    table.CheckConstraint("CK_VendorInstallationIdentities_KeyVersion", "[KeyVersion] >= 1");
                    table.CheckConstraint("CK_VendorInstallationIdentities_SoftwareConsent",
                        "([AssuranceLevel] = 'SoftwareProtected' AND [SoftwareProviderRiskAcceptedAtUtc] IS NOT NULL AND [SoftwareProviderRiskAcceptedBy] IS NOT NULL AND [SoftwareProviderConsentReceiptJson] IS NOT NULL) OR ([AssuranceLevel] = 'HardwareTpm' AND [SoftwareProviderRiskAcceptedAtUtc] IS NULL AND [SoftwareProviderRiskAcceptedBy] IS NULL AND [SoftwareProviderConsentReceiptJson] IS NULL)");
                    table.CheckConstraint("CK_VendorInstallationIdentities_State", "[State] IN (N'Current', N'Retired')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorInstallationIdentities_InstallationId_KeyVersion",
                table: "VendorInstallationIdentities",
                columns: new[] { "InstallationId", "KeyVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorInstallationIdentities_KeyId",
                table: "VendorInstallationIdentities",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorInstallationIdentities_PublicKeyFingerprint",
                table: "VendorInstallationIdentities",
                column: "PublicKeyFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorInstallationIdentities_InstallationId",
                table: "VendorInstallationIdentities",
                column: "InstallationId",
                unique: true,
                filter: "[State] = N'Current'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VendorInstallationIdentities");
        }
    }
}
