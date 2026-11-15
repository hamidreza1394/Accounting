using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faranou.Vendor.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VendorLicenseCompanyEntitlementsDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyEntitlementsDraftJson",
                table: "VendorLicenses",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyEntitlementsDraftJson",
                table: "VendorLicenses");
        }
    }
}
