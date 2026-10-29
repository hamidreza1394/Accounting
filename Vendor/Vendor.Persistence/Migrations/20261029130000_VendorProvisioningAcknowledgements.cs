using Faranou.Vendor.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

[DbContext(typeof(VendorDbContext))]
[Migration("20261029130000_VendorProvisioningAcknowledgements")]
public partial class VendorProvisioningAcknowledgements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "AcknowledgedAtUtc",
            table: "VendorProvisioningRequests",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AcknowledgementKeyId",
            table: "VendorProvisioningRequests",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AcknowledgementResult",
            table: "VendorProvisioningRequests",
            type: "varchar(16)",
            unicode: false,
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AcknowledgementSha256",
            table: "VendorProvisioningRequests",
            type: "char(64)",
            unicode: false,
            maxLength: 64,
            fixedLength: true,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AcknowledgedAtUtc", table: "VendorProvisioningRequests");
        migrationBuilder.DropColumn(name: "AcknowledgementKeyId", table: "VendorProvisioningRequests");
        migrationBuilder.DropColumn(name: "AcknowledgementResult", table: "VendorProvisioningRequests");
        migrationBuilder.DropColumn(name: "AcknowledgementSha256", table: "VendorProvisioningRequests");
    }
}
