using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

public partial class VendorLicenseOperationWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VendorLicenseOperationRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                ExpectedLicenseRowVersion = table.Column<byte[]>(type: "binary(8)", nullable: false),
                ExpectedCustomerRowVersion = table.Column<byte[]>(type: "binary(8)", nullable: false),
                ExpectedInstallationRowVersion = table.Column<byte[]>(type: "binary(8)", nullable: true),
                ExpectedContractRowVersion = table.Column<byte[]>(type: "binary(8)", nullable: true),
                RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ReviewComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VendorLicenseOperationRequests", x => x.Id);
                table.CheckConstraint("CK_VendorLicenseOperationRequests_Action", "[Action] IN (N'Issue', N'Revoke')");
                table.CheckConstraint("CK_VendorLicenseOperationRequests_Status", "[Status] IN (N'Pending', N'Approved', N'Rejected', N'Stale')");
                table.ForeignKey(
                    name: "FK_VendorLicenseOperationRequests_VendorLicenses_LicenseId",
                    column: x => x.LicenseId,
                    principalTable: "VendorLicenses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "VendorLicenseArtifacts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OperationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ArtifactType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                Kid = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Algorithm = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                EnvelopeJson = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VendorLicenseArtifacts", x => x.Id);
                table.CheckConstraint("CK_VendorLicenseArtifacts_ArtifactType", "[ArtifactType] IN (N'License', N'Revocation')");
                table.CheckConstraint("CK_VendorLicenseArtifacts_EnvelopeJson", "ISJSON([EnvelopeJson]) = 1");
                table.CheckConstraint("CK_VendorLicenseArtifacts_Sequence", "[Sequence] >= 1");
                table.ForeignKey(
                    name: "FK_VendorLicenseArtifacts_VendorLicenses_LicenseId",
                    column: x => x.LicenseId,
                    principalTable: "VendorLicenses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VendorLicenseArtifacts_VendorLicenseOperationRequests_OperationRequestId",
                    column: x => x.OperationRequestId,
                    principalTable: "VendorLicenseOperationRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VendorLicenseOperationRequests_LicenseId",
            table: "VendorLicenseOperationRequests",
            column: "LicenseId",
            unique: true,
            filter: "[Status] = N'Pending'");
        migrationBuilder.CreateIndex(
            name: "IX_VendorLicenseOperationRequests_RequestedByUserId",
            table: "VendorLicenseOperationRequests",
            column: "RequestedByUserId");
        migrationBuilder.CreateIndex(
            name: "IX_VendorLicenseOperationRequests_ReviewedByUserId",
            table: "VendorLicenseOperationRequests",
            column: "ReviewedByUserId");
        migrationBuilder.CreateIndex(
            name: "IX_VendorLicenseOperationRequests_Status_RequestedAtUtc",
            table: "VendorLicenseOperationRequests",
            columns: new[] { "Status", "RequestedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_VendorLicenseArtifacts_CreatedAtUtc",
            table: "VendorLicenseArtifacts",
            column: "CreatedAtUtc");
        migrationBuilder.CreateIndex(
            name: "IX_VendorLicenseArtifacts_LicenseId_ArtifactType_Sequence",
            table: "VendorLicenseArtifacts",
            columns: new[] { "LicenseId", "ArtifactType", "Sequence" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_VendorLicenseArtifacts_OperationRequestId",
            table: "VendorLicenseArtifacts",
            column: "OperationRequestId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VendorLicenseArtifacts");
        migrationBuilder.DropTable(name: "VendorLicenseOperationRequests");
    }
}
