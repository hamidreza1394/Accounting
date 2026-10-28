using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

public partial class VendorProvisioningRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VendorProvisioningRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                Nonce = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PayloadSha256 = table.Column<string>(type: "char(64)", unicode: false, maxLength: 64, fixedLength: true, nullable: false),
                Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                KeyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                Algorithm = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                SignatureBase64 = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                SignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                FailureCode = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VendorProvisioningRequests", x => x.Id);
                table.CheckConstraint("CK_VendorProvisioningRequests_Status",
                    "[Status] IN (N'AwaitingSigner', N'PendingDelivery', N'Confirmed', N'Failed')");
                table.ForeignKey(
                    name: "FK_VendorProvisioningRequests_VendorCustomers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "VendorCustomers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VendorProvisioningRequests_VendorInstallations_InstallationId",
                    column: x => x.InstallationId,
                    principalTable: "VendorInstallations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VendorProvisioningRequests_CustomerId",
            table: "VendorProvisioningRequests",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_VendorProvisioningRequests_InstallationId",
            table: "VendorProvisioningRequests",
            column: "InstallationId");

        migrationBuilder.CreateIndex(
            name: "IX_VendorProvisioningRequests_Nonce",
            table: "VendorProvisioningRequests",
            column: "Nonce",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VendorProvisioningRequests_RequestId",
            table: "VendorProvisioningRequests",
            column: "RequestId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VendorProvisioningRequests");
    }
}
