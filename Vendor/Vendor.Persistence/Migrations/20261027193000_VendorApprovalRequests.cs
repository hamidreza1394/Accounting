using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

public partial class VendorApprovalRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VendorApprovalRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ResourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ExpectedResourceRowVersion = table.Column<byte[]>(type: "binary(8)", nullable: false),
                RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ReviewComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_VendorApprovalRequests", item => item.Id));

        migrationBuilder.CreateIndex(
            name: "IX_VendorApprovalRequests_RequestedByUserId",
            table: "VendorApprovalRequests",
            column: "RequestedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_VendorApprovalRequests_ReviewedByUserId",
            table: "VendorApprovalRequests",
            column: "ReviewedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_VendorApprovalRequests_ResourceType_ResourceId",
            table: "VendorApprovalRequests",
            columns: new[] { "ResourceType", "ResourceId" },
            unique: true,
            filter: "[Status] = N'Pending'");

        migrationBuilder.CreateIndex(
            name: "IX_VendorApprovalRequests_Status_RequestedAtUtc",
            table: "VendorApprovalRequests",
            columns: new[] { "Status", "RequestedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VendorApprovalRequests");
    }
}
