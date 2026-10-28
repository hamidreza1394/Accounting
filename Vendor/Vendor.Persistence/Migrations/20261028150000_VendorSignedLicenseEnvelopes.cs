using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

public partial class VendorSignedLicenseEnvelopes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VendorSignedLicenseEnvelopes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                KeyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Algorithm = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                EnvelopeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PayloadSha256 = table.Column<string>(type: "char(64)", unicode: false, maxLength: 64, fixedLength: true, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VendorSignedLicenseEnvelopes", x => x.Id);
                table.CheckConstraint("CK_VendorSignedLicenseEnvelopes_Sequence", "[Sequence] >= 1");
                table.ForeignKey(
                    name: "FK_VendorSignedLicenseEnvelopes_VendorLicenses_LicenseId",
                    column: x => x.LicenseId,
                    principalTable: "VendorLicenses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VendorSignedLicenseEnvelopes_LicenseId_Sequence",
            table: "VendorSignedLicenseEnvelopes",
            columns: new[] { "LicenseId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VendorSignedLicenseEnvelopes_PayloadSha256",
            table: "VendorSignedLicenseEnvelopes",
            column: "PayloadSha256");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VendorSignedLicenseEnvelopes");
    }
}
