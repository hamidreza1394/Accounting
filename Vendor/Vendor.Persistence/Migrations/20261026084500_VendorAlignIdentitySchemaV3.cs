using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

public sealed partial class VendorAlignIdentitySchemaV3 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "VendorAdminUserTokens",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(450)");

        migrationBuilder.AlterColumn<string>(
            name: "LoginProvider",
            table: "VendorAdminUserTokens",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(450)");

        migrationBuilder.AlterColumn<string>(
            name: "PhoneNumber",
            table: "VendorAdminUsers",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ProviderKey",
            table: "VendorAdminUserLogins",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(450)");

        migrationBuilder.AlterColumn<string>(
            name: "LoginProvider",
            table: "VendorAdminUserLogins",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(450)");

        migrationBuilder.CreateIndex(
            name: "IX_VendorAdminUserPasskeys_UserId",
            table: "VendorAdminUserPasskeys",
            column: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_VendorAdminUserPasskeys_VendorAdminUsers_UserId",
            table: "VendorAdminUserPasskeys",
            column: "UserId",
            principalTable: "VendorAdminUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_VendorAdminUserPasskeys_VendorAdminUsers_UserId",
            table: "VendorAdminUserPasskeys");

        migrationBuilder.DropIndex(
            name: "IX_VendorAdminUserPasskeys_UserId",
            table: "VendorAdminUserPasskeys");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "VendorAdminUserTokens",
            type: "nvarchar(450)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(128)",
            oldMaxLength: 128);

        migrationBuilder.AlterColumn<string>(
            name: "LoginProvider",
            table: "VendorAdminUserTokens",
            type: "nvarchar(450)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(128)",
            oldMaxLength: 128);

        migrationBuilder.AlterColumn<string>(
            name: "PhoneNumber",
            table: "VendorAdminUsers",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(256)",
            oldMaxLength: 256,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ProviderKey",
            table: "VendorAdminUserLogins",
            type: "nvarchar(450)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(128)",
            oldMaxLength: 128);

        migrationBuilder.AlterColumn<string>(
            name: "LoginProvider",
            table: "VendorAdminUserLogins",
            type: "nvarchar(450)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(128)",
            oldMaxLength: 128);
    }
}
