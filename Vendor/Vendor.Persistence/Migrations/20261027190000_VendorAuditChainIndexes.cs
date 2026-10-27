using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

public partial class VendorAuditChainIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_VendorAuditEvents_EventHash",
            table: "VendorAuditEvents",
            column: "EventHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VendorAuditEvents_PreviousHash",
            table: "VendorAuditEvents",
            column: "PreviousHash");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_VendorAuditEvents_PreviousHash",
            table: "VendorAuditEvents");

        migrationBuilder.DropIndex(
            name: "IX_VendorAuditEvents_EventHash",
            table: "VendorAuditEvents");
    }
}
