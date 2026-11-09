using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeUserEmailAddressOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_EmailAddress",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "EmailAddress",
                table: "Users",
                type: "varchar(254)",
                unicode: false,
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(254)",
                oldUnicode: false,
                oldMaxLength: 254);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailAddress",
                table: "Users",
                column: "EmailAddress",
                unique: true,
                filter: "[EmailAddress] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_EmailAddress",
                table: "Users");

            // Users created without an e-mail would all become '' and break the unique index below.
            // Give each one a distinct, clearly synthetic placeholder instead (no user data is lost:
            // the address was NULL before).
            migrationBuilder.Sql("UPDATE [Users] SET [EmailAddress] = CONCAT('no-email-', CONVERT(varchar(36), [Id]), '@invalid.local') WHERE [EmailAddress] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "EmailAddress",
                table: "Users",
                type: "varchar(254)",
                unicode: false,
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(254)",
                oldUnicode: false,
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailAddress",
                table: "Users",
                column: "EmailAddress",
                unique: true);
        }
    }
}
