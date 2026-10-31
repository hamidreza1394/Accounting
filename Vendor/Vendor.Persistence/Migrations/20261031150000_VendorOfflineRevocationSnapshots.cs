using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Faranou.Vendor.Persistence;

namespace Faranou.Vendor.Persistence.Migrations;

[DbContext(typeof(VendorDbContext))]
[Migration("20261031150000_VendorOfflineRevocationSnapshots")]
public partial class VendorOfflineRevocationSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VendorOfflineRevocationSequences",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false),
                LastSequence = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VendorOfflineRevocationSequences", x => x.Id);
                table.CheckConstraint("CK_VendorOfflineRevocationSequences_Singleton", "[Id] = 1");
                table.CheckConstraint("CK_VendorOfflineRevocationSequences_LastSequence", "[LastSequence] >= 0");
            });

        migrationBuilder.InsertData(
            table: "VendorOfflineRevocationSequences",
            columns: new[] { "Id", "LastSequence" },
            values: new object[] { 1, 0L });

        migrationBuilder.CreateTable(
            name: "VendorOfflineRevocationSnapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false),
                ListId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                KeyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Algorithm = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                EnvelopeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PayloadSha256 = table.Column<string>(type: "char(64)", unicode: false, maxLength: 64, fixedLength: true, nullable: false),
                IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VendorOfflineRevocationSnapshots", x => x.Id);
                table.CheckConstraint("CK_VendorOfflineRevocationSnapshots_Sequence", "[Sequence] >= 1");
                table.CheckConstraint("CK_VendorOfflineRevocationSnapshots_Expiry", "[ExpiresAtUtc] > [IssuedAtUtc]");
            });

        migrationBuilder.CreateIndex(
            name: "IX_VendorOfflineRevocationSnapshots_Sequence",
            table: "VendorOfflineRevocationSnapshots",
            column: "Sequence",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VendorOfflineRevocationSnapshots_ListId",
            table: "VendorOfflineRevocationSnapshots",
            column: "ListId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VendorOfflineRevocationSnapshots_PayloadSha256",
            table: "VendorOfflineRevocationSnapshots",
            column: "PayloadSha256");

        migrationBuilder.Sql("""
            CREATE TRIGGER [dbo].[TR_VendorOfflineRevocationSnapshots_Immutable]
            ON [dbo].[VendorOfflineRevocationSnapshots]
            INSTEAD OF UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51000, 'Vendor offline revocation snapshots are immutable.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_VendorOfflineRevocationSnapshots_Immutable];");
        migrationBuilder.DropTable(name: "VendorOfflineRevocationSnapshots");
        migrationBuilder.DeleteData(
            table: "VendorOfflineRevocationSequences",
            keyColumn: "Id",
            keyValue: 1);
        migrationBuilder.DropTable(name: "VendorOfflineRevocationSequences");
    }
}
