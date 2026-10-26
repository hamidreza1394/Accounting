using Faranou.Vendor.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

[DbContext(typeof(VendorDbContext))]
[Migration("20261026045412_VendorAddPasskeySupport")]
public sealed class VendorAddPasskeySupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VendorAdminUserPasskeys",
            columns: table => new
            {
                CredentialId = table.Column<byte[]>(type: "varbinary(1024)", maxLength: 1024, nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Data = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_VendorAdminUserPasskeys", x => x.CredentialId));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VendorAdminUserPasskeys");
    }
}
