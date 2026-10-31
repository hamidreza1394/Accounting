using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

public partial class VendorSignedRevocationSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VendorSignedRevocationSnapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TriggeringLicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false),
                ListId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                KeyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Algorithm = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                EnvelopeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PayloadSha256 = table.Column<string>(type: "char(64)", unicode: false, maxLength: 64, fixedLength: true, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false,
                    defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VendorSignedRevocationSnapshots", item => item.Id);
                table.CheckConstraint("CK_VendorSignedRevocationSnapshots_Sequence", "[Sequence] >= 1");
                table.ForeignKey(
                    name: "FK_VendorSignedRevocationSnapshots_VendorApprovalRequests_ApprovalRequestId",
                    column: item => item.ApprovalRequestId,
                    principalTable: "VendorApprovalRequests",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VendorSignedRevocationSnapshots_VendorLicenses_TriggeringLicenseId",
                    column: item => item.TriggeringLicenseId,
                    principalTable: "VendorLicenses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VendorSignedRevocationSnapshots_ApprovalRequestId",
            table: "VendorSignedRevocationSnapshots",
            column: "ApprovalRequestId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_VendorSignedRevocationSnapshots_ListId",
            table: "VendorSignedRevocationSnapshots",
            column: "ListId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_VendorSignedRevocationSnapshots_Sequence",
            table: "VendorSignedRevocationSnapshots",
            column: "Sequence",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_VendorSignedRevocationSnapshots_TriggeringLicenseId",
            table: "VendorSignedRevocationSnapshots",
            column: "TriggeringLicenseId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VendorSignedRevocationSnapshots");
    }
}
