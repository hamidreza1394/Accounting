using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerLicensePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstallationIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InstallationId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    KeyId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    KeyVersion = table.Column<int>(type: "int", nullable: false),
                    ProviderName = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    AssuranceLevel = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    OsInstallationMarkerDigest = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    PublicKeyFingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    PublicKeySubjectPublicKeyInfoBase64 = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: false),
                    SoftwareProviderRiskAcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SoftwareProviderRiskAcceptedBy = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: true),
                    State = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                    ProvisionedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastVerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallationIdentities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstalledLicenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InstallationIdentityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallationId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    LicenseId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    FormatVersion = table.Column<int>(type: "int", nullable: false),
                    Kid = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    Algorithm = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignatureBase64 = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    EnvelopeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadDigest = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    State = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstalledLicenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstalledLicenses_InstallationIdentities_InstallationIdentityId",
                        column: x => x.InstallationIdentityId,
                        principalTable: "InstallationIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstallationIdentities_InstallationId_KeyVersion",
                table: "InstallationIdentities",
                columns: new[] { "InstallationId", "KeyVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstallationIdentities_KeyId",
                table: "InstallationIdentities",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstallationIdentities_InstallationId",
                table: "InstallationIdentities",
                column: "InstallationId",
                unique: true,
                filter: "[State] = 'Current'");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledLicenses_InstallationId_Sequence",
                table: "InstalledLicenses",
                columns: new[] { "InstallationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstalledLicenses_LicenseId_Sequence",
                table: "InstalledLicenses",
                columns: new[] { "LicenseId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstalledLicenses_InstallationId",
                table: "InstalledLicenses",
                column: "InstallationId",
                unique: true,
                filter: "[State] = 'Current'");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledLicenses_InstallationIdentityId",
                table: "InstalledLicenses",
                column: "InstallationIdentityId");

            migrationBuilder.Sql("ALTER TABLE [InstallationIdentities] ADD CONSTRAINT [CK_InstallationIdentities_AssuranceLevel] CHECK ([AssuranceLevel] IN ('HardwareTpm', 'SoftwareProtected'));");
            migrationBuilder.Sql("ALTER TABLE [InstallationIdentities] ADD CONSTRAINT [CK_InstallationIdentities_State] CHECK ([State] IN ('Current', 'Retired', 'ReactivationRequired'));");
            migrationBuilder.Sql("ALTER TABLE [InstallationIdentities] ADD CONSTRAINT [CK_InstallationIdentities_SoftwareConsent] CHECK (([AssuranceLevel] <> 'SoftwareProtected' OR ([SoftwareProviderRiskAcceptedAtUtc] IS NOT NULL AND [SoftwareProviderRiskAcceptedBy] IS NOT NULL)) AND ([AssuranceLevel] <> 'HardwareTpm' OR ([SoftwareProviderRiskAcceptedAtUtc] IS NULL AND [SoftwareProviderRiskAcceptedBy] IS NULL)));");
            migrationBuilder.Sql("ALTER TABLE [InstalledLicenses] ADD CONSTRAINT [CK_InstalledLicenses_PositiveSequence] CHECK ([Sequence] > 0);");
            migrationBuilder.Sql("ALTER TABLE [InstalledLicenses] ADD CONSTRAINT [CK_InstalledLicenses_PositiveFormatVersion] CHECK ([FormatVersion] > 0);");
            migrationBuilder.Sql("ALTER TABLE [InstalledLicenses] ADD CONSTRAINT [CK_InstalledLicenses_State] CHECK ([State] IN ('Current', 'Superseded'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InstalledLicenses");
            migrationBuilder.DropTable(name: "InstallationIdentities");
        }
    }
}
