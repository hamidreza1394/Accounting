using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faranou.Vendor.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VendorLicenseAmendmentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorLicenseAmendmentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    RequestedMaxCompanies = table.Column<int>(type: "int", nullable: true),
                    RequestedSupportUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RequestedUpdatesUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureCode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorLicenseAmendmentRequests", x => x.Id);
                    table.CheckConstraint("CK_VendorLicenseAmendmentRequests_Kind", "[Kind] IN (N'maxCompanies', N'supportUpdates')");
                    table.CheckConstraint("CK_VendorLicenseAmendmentRequests_Status", "[Status] IN (N'Requested', N'PendingApproval', N'Issued', N'Rejected', N'Failed')");
                    table.ForeignKey(
                        name: "FK_VendorLicenseAmendmentRequests_VendorCustomers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "VendorCustomers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorLicenseAmendmentRequests_VendorLicenses_LicenseId",
                        column: x => x.LicenseId,
                        principalTable: "VendorLicenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorLicenseAmendmentRequests_VendorSalesOrderLines_OrderLineId",
                        column: x => x.OrderLineId,
                        principalTable: "VendorSalesOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorLicenseAmendmentRequests_VendorSalesOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "VendorSalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorLicenseAmendmentRequests_ApprovalRequestId",
                table: "VendorLicenseAmendmentRequests",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorLicenseAmendmentRequests_CustomerId",
                table: "VendorLicenseAmendmentRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorLicenseAmendmentRequests_LicenseId",
                table: "VendorLicenseAmendmentRequests",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorLicenseAmendmentRequests_OrderId",
                table: "VendorLicenseAmendmentRequests",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorLicenseAmendmentRequests_OrderLineId",
                table: "VendorLicenseAmendmentRequests",
                column: "OrderLineId",
                unique: true,
                filter: "[Status] IN (N'Requested', N'PendingApproval', N'Issued')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorLicenseAmendmentRequests");
        }
    }
}
