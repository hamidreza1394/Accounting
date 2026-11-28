using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faranou.Vendor.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VendorLicenseAmendmentDeliveryAcknowledgement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorLicenseAmendmentRequests_OrderLineId",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VendorLicenseAmendmentRequests_Status",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcknowledgedAtUtc",
                table: "VendorLicenseAmendmentRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementErrorCode",
                table: "VendorLicenseAmendmentRequests",
                type: "varchar(128)",
                unicode: false,
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementKeyId",
                table: "VendorLicenseAmendmentRequests",
                type: "varchar(128)",
                unicode: false,
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementResult",
                table: "VendorLicenseAmendmentRequests",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementSha256",
                table: "VendorLicenseAmendmentRequests",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CustomerProcessedAtUtc",
                table: "VendorLicenseAmendmentRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAtUtc",
                table: "VendorLicenseAmendmentRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveredNonce",
                table: "VendorLicenseAmendmentRequests",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveredPayloadSha256",
                table: "VendorLicenseAmendmentRequests",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IssuedSequence",
                table: "VendorLicenseAmendmentRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorLicenseAmendmentRequests_OrderLineId",
                table: "VendorLicenseAmendmentRequests",
                column: "OrderLineId",
                unique: true,
                filter: "[Status] IN ('Requested', 'PendingApproval', 'Issued', 'Delivered')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VendorLicenseAmendmentRequests_Status",
                table: "VendorLicenseAmendmentRequests",
                sql: "[Status] IN (N'Requested', N'PendingApproval', N'Issued', N'Delivered', N'Applied', N'Rejected', N'Failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorLicenseAmendmentRequests_OrderLineId",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VendorLicenseAmendmentRequests_Status",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAtUtc",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropColumn(
                name: "AcknowledgementErrorCode",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropColumn(
                name: "AcknowledgementKeyId",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropColumn(
                name: "AcknowledgementResult",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropColumn(
                name: "AcknowledgementSha256",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropColumn(
                name: "CustomerProcessedAtUtc",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropColumn(
                name: "DeliveredAtUtc",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropColumn(
                name: "DeliveredNonce",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropColumn(
                name: "DeliveredPayloadSha256",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.DropColumn(
                name: "IssuedSequence",
                table: "VendorLicenseAmendmentRequests");

            migrationBuilder.CreateIndex(
                name: "IX_VendorLicenseAmendmentRequests_OrderLineId",
                table: "VendorLicenseAmendmentRequests",
                column: "OrderLineId",
                unique: true,
                filter: "[Status] IN ('Requested', 'PendingApproval', 'Issued')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VendorLicenseAmendmentRequests_Status",
                table: "VendorLicenseAmendmentRequests",
                sql: "[Status] IN (N'Requested', N'PendingApproval', N'Issued', N'Rejected', N'Failed')");
        }
    }
}
