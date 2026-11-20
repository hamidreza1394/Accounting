using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faranou.Vendor.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VendorSalesOrdersAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorSalesOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorSalesOrders", x => x.Id);
                    table.CheckConstraint("CK_VendorSalesOrders_TotalAmount", "[TotalAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_VendorSalesOrders_VendorContracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "VendorContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorSalesOrders_VendorCustomers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "VendorCustomers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorPaymentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClaimedPaidAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    VerifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorPaymentRecords", x => x.Id);
                    table.CheckConstraint("CK_VendorPaymentRecords_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_VendorPaymentRecords_Status", "[Status] IN (N'Recorded', N'Verified', N'Rejected')");
                    table.ForeignKey(
                        name: "FK_VendorPaymentRecords_VendorSalesOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "VendorSalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorSalesOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    LicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "CONVERT(datetimeoffset, SYSUTCDATETIME())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorSalesOrderLines", x => x.Id);
                    table.CheckConstraint("CK_VendorSalesOrderLines_Amounts", "[UnitAmount] >= 0 AND [LineAmount] >= 0");
                    table.CheckConstraint("CK_VendorSalesOrderLines_Quantity", "[Quantity] >= 1");
                    table.ForeignKey(
                        name: "FK_VendorSalesOrderLines_VendorSalesOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "VendorSalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorPaymentRecords_OrderId",
                table: "VendorPaymentRecords",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPaymentRecords_Status_OrderId",
                table: "VendorPaymentRecords",
                columns: new[] { "Status", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorSalesOrderLines_OrderId",
                table: "VendorSalesOrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorSalesOrders_ContractId",
                table: "VendorSalesOrders",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorSalesOrders_CustomerId",
                table: "VendorSalesOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorSalesOrders_CustomerId_OrderNumber",
                table: "VendorSalesOrders",
                columns: new[] { "CustomerId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorSalesOrders_DueAtUtc",
                table: "VendorSalesOrders",
                column: "DueAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorPaymentRecords");

            migrationBuilder.DropTable(
                name: "VendorSalesOrderLines");

            migrationBuilder.DropTable(
                name: "VendorSalesOrders");
        }
    }
}
