using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

[DbContext(typeof(VendorDbContext))]
[Migration("20261027060000_VendorBusinessDomain")]
public sealed class VendorBusinessDomain : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VendorCustomers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                LegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_VendorCustomers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "VendorContracts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VendorContracts", x => x.Id);
                table.ForeignKey("FK_VendorContracts_VendorCustomers_CustomerId", x => x.CustomerId, "VendorCustomers", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "VendorInstallations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InstallationCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Environment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VendorInstallations", x => x.Id);
                table.ForeignKey("FK_VendorInstallations_VendorCustomers_CustomerId", x => x.CustomerId, "VendorCustomers", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "VendorLicenses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                InstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LicenseNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ProductCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ValidFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ValidUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VendorLicenses", x => x.Id);
                table.ForeignKey("FK_VendorLicenses_VendorCustomers_CustomerId", x => x.CustomerId, "VendorCustomers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_VendorLicenses_VendorContracts_ContractId", x => x.ContractId, "VendorContracts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_VendorLicenses_VendorInstallations_InstallationId", x => x.InstallationId, "VendorInstallations", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_VendorCustomers_CustomerNumber", "VendorCustomers", "CustomerNumber", unique: true);
        migrationBuilder.CreateIndex("IX_VendorContracts_CustomerId", "VendorContracts", "CustomerId");
        migrationBuilder.CreateIndex("IX_VendorContracts_CustomerId_ContractNumber", "VendorContracts", new[] { "CustomerId", "ContractNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_VendorInstallations_CustomerId", "VendorInstallations", "CustomerId");
        migrationBuilder.CreateIndex("IX_VendorInstallations_InstallationCode", "VendorInstallations", "InstallationCode", unique: true);
        migrationBuilder.CreateIndex("IX_VendorLicenses_ContractId", "VendorLicenses", "ContractId");
        migrationBuilder.CreateIndex("IX_VendorLicenses_CustomerId", "VendorLicenses", "CustomerId");
        migrationBuilder.CreateIndex("IX_VendorLicenses_InstallationId", "VendorLicenses", "InstallationId");
        migrationBuilder.CreateIndex("IX_VendorLicenses_LicenseNumber", "VendorLicenses", "LicenseNumber", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VendorLicenses");
        migrationBuilder.DropTable(name: "VendorInstallations");
        migrationBuilder.DropTable(name: "VendorContracts");
        migrationBuilder.DropTable(name: "VendorCustomers");
    }
}
