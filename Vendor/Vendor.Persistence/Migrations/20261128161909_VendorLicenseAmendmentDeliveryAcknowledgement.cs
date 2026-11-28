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

            // Legacy rows issued before this migration carry no IssuedSequence. Bind an Issued
            // request to the license's last issued envelope only when it is the ONLY active
            // amendment on that license, which makes the (LicenseId, Sequence) binding
            // unambiguous. Every other legacy row stays NULL and delivery fails closed with
            // amendment_envelope_missing instead of exporting a possibly different envelope.
            migrationBuilder.Sql("""
                UPDATE r
                SET r.IssuedSequence = l.LastIssuedSequence
                FROM VendorLicenseAmendmentRequests AS r
                INNER JOIN VendorLicenses AS l ON l.Id = r.LicenseId
                WHERE r.[Status] = N'Issued'
                  AND r.IssuedSequence IS NULL
                  AND l.LastIssuedSequence > 0
                  AND EXISTS (
                      SELECT 1 FROM VendorSignedLicenseEnvelopes AS e
                      WHERE e.LicenseId = r.LicenseId AND e.Sequence = l.LastIssuedSequence)
                  AND NOT EXISTS (
                      SELECT 1 FROM VendorLicenseAmendmentRequests AS other
                      WHERE other.LicenseId = r.LicenseId
                        AND other.Id <> r.Id
                        AND other.[Status] IN (N'Requested', N'PendingApproval', N'Issued', N'Delivered'))
                """);

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
            // Fail closed BEFORE destroying any column: delivered or applied amendments carry
            // signed delivery/acknowledgement history that cannot be reversed honestly. Rolling
            // back would either fail later on the narrowed status check constraint or silently
            // rewrite a delivered/applied amendment back to Issued, which is never acceptable.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM VendorLicenseAmendmentRequests
                    WHERE [Status] IN (N'Delivered', N'Applied'))
                    THROW 51000,
                        'Rollback blocked: delivered or applied license amendment rows exist. Their signed delivery/acknowledgement history cannot be reversed safely; resolve those rows first, then re-run this rollback.',
                        1;
                """);

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
