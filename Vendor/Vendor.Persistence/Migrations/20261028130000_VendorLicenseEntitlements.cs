using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

public partial class VendorLicenseEntitlements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "GraceDays",
            table: "VendorLicenses",
            type: "int",
            nullable: false,
            defaultValue: 30);

        migrationBuilder.AddColumn<string>(
            name: "LicenseKind",
            table: "VendorLicenses",
            type: "nvarchar(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "perpetual");

        migrationBuilder.AddColumn<int>(
            name: "MaxCompanies",
            table: "VendorLicenses",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "LastIssuedSequence",
            table: "VendorLicenses",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "SubsystemsJson",
            table: "VendorLicenses",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: false,
            defaultValue: "[]");

        // Preserve the meaning of existing records without guessing a perpetual expiry date.
        migrationBuilder.Sql("""
            UPDATE dbo.VendorLicenses
            SET LicenseKind = CASE WHEN ValidUntilUtc IS NULL THEN N'perpetual' ELSE N'term' END;
            """);

        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorLicenses_GraceDays",
            table: "VendorLicenses",
            sql: "[GraceDays] >= 0 AND [GraceDays] <= 90");

        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorLicenses_LastIssuedSequence",
            table: "VendorLicenses",
            sql: "[LastIssuedSequence] >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorLicenses_LicenseKind",
            table: "VendorLicenses",
            sql: "[LicenseKind] IN (N'term', N'perpetual')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorLicenses_KindExpiry",
            table: "VendorLicenses",
            sql: "([LicenseKind] = N'term' AND [ValidUntilUtc] IS NOT NULL) OR ([LicenseKind] = N'perpetual' AND [ValidUntilUtc] IS NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorLicenses_MaxCompanies",
            table: "VendorLicenses",
            sql: "[MaxCompanies] IS NULL OR [MaxCompanies] >= 1");

        migrationBuilder.AddCheckConstraint(
            name: "CK_VendorLicenses_SubsystemsJson",
            table: "VendorLicenses",
            sql: "ISJSON([SubsystemsJson]) = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_VendorLicenses_SubsystemsJson", "VendorLicenses");
        migrationBuilder.DropCheckConstraint("CK_VendorLicenses_MaxCompanies", "VendorLicenses");
        migrationBuilder.DropCheckConstraint("CK_VendorLicenses_KindExpiry", "VendorLicenses");
        migrationBuilder.DropCheckConstraint("CK_VendorLicenses_LicenseKind", "VendorLicenses");
        migrationBuilder.DropCheckConstraint("CK_VendorLicenses_LastIssuedSequence", "VendorLicenses");
        migrationBuilder.DropCheckConstraint("CK_VendorLicenses_GraceDays", "VendorLicenses");

        migrationBuilder.DropColumn("GraceDays", "VendorLicenses");
        migrationBuilder.DropColumn("LicenseKind", "VendorLicenses");
        migrationBuilder.DropColumn("MaxCompanies", "VendorLicenses");
        migrationBuilder.DropColumn("LastIssuedSequence", "VendorLicenses");
        migrationBuilder.DropColumn("SubsystemsJson", "VendorLicenses");
    }
}
