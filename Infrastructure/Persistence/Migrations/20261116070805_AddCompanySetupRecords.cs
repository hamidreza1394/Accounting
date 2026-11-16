using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySetupRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanySetupRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyReference = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    InstallationId = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySetupRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanySetupRecords_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Company references are vendor-signed identities: the mapping must compare them
            // binary (vendor review L-2), so the column is re-collated before its unique index
            // is built. The index below then inherits the binary collation.
            migrationBuilder.Sql("ALTER TABLE [CompanySetupRecords] ALTER COLUMN [CompanyReference] nvarchar(450) COLLATE Latin1_General_BIN2 NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySetupRecords_CompanyId",
                table: "CompanySetupRecords",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanySetupRecords_CompanyReference",
                table: "CompanySetupRecords",
                column: "CompanyReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanySetupRecords");
        }
    }
}
