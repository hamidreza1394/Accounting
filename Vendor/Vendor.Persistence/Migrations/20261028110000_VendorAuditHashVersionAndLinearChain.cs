using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

public partial class VendorAuditHashVersionAndLinearChain : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "HashVersion",
            table: "VendorAuditEvents",
            type: "int",
            nullable: false,
            defaultValue: 1);

        // Fail closed before changing the index: do not silently repair or discard a forked audit history.
        migrationBuilder.Sql("""
            DECLARE @GenesisHash char(64) = '0000000000000000000000000000000000000000000000000000000000000000';

            IF EXISTS (
                SELECT PreviousHash
                FROM dbo.VendorAuditEvents
                GROUP BY PreviousHash
                HAVING COUNT_BIG(*) > 1
            )
                THROW 51001, 'Vendor audit chain contains duplicate parent hashes; migration refused.', 1;

            IF EXISTS (SELECT 1 FROM dbo.VendorAuditEvents)
               AND (SELECT COUNT_BIG(*) FROM dbo.VendorAuditEvents WHERE PreviousHash = @GenesisHash) <> 1
                THROW 51002, 'Vendor audit chain must contain exactly one genesis root; migration refused.', 1;

            IF EXISTS (
                SELECT 1
                FROM dbo.VendorAuditEvents AS child
                WHERE child.PreviousHash <> @GenesisHash
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.VendorAuditEvents AS parent
                      WHERE parent.EventHash = child.PreviousHash
                  )
            )
                THROW 51003, 'Vendor audit chain contains an event with a missing parent; migration refused.', 1;

            IF EXISTS (SELECT 1 FROM dbo.VendorAuditEvents)
            BEGIN
                WITH Reachable AS (
                    SELECT EventHash
                    FROM dbo.VendorAuditEvents
                    WHERE PreviousHash = @GenesisHash
                    UNION ALL
                    SELECT child.EventHash
                    FROM dbo.VendorAuditEvents AS child
                    INNER JOIN Reachable AS parent ON child.PreviousHash = parent.EventHash
                )
                SELECT EventHash INTO #VendorAuditReachable FROM Reachable OPTION (MAXRECURSION 0);

                IF (SELECT COUNT_BIG(*) FROM #VendorAuditReachable) <> (SELECT COUNT_BIG(*) FROM dbo.VendorAuditEvents)
                    THROW 51004, 'Vendor audit chain contains disconnected or cyclic events; migration refused.', 1;

                DROP TABLE #VendorAuditReachable;
            END;
            """);

        migrationBuilder.DropIndex(
            name: "IX_VendorAuditEvents_PreviousHash",
            table: "VendorAuditEvents");

        migrationBuilder.CreateIndex(
            name: "IX_VendorAuditEvents_PreviousHash",
            table: "VendorAuditEvents",
            column: "PreviousHash",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_VendorAuditEvents_PreviousHash",
            table: "VendorAuditEvents");

        migrationBuilder.CreateIndex(
            name: "IX_VendorAuditEvents_PreviousHash",
            table: "VendorAuditEvents",
            column: "PreviousHash");

        migrationBuilder.DropColumn(
            name: "HashVersion",
            table: "VendorAuditEvents");
    }
}
