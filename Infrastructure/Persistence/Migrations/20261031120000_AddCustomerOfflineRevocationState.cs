using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerOfflineRevocationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerOfflineRevocationStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotDigest = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedLicenseIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnchorGeneration = table.Column<long>(type: "bigint", nullable: false),
                    PendingEnvelopeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PendingExpectedGeneration = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOfflineRevocationStates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerOfflineRevocationStates");
        }
    }
}
