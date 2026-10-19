using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <summary>
    /// Widens <c>Users.Password</c> from <c>varchar(44)</c> (a Base64 SHA-256 digest) to
    /// <c>varchar(255)</c>, so a credential produced by
    /// <c>Microsoft.AspNetCore.Identity.PasswordHasher&lt;TUser&gt;</c> (84 characters) fits.
    /// Existing rows are not modified: a legacy digest stays valid and is upgraded in place
    /// after the next successful sign-in.
    /// </summary>
    public partial class WidenUserPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Users",
                type: "varchar(255)",
                unicode: false,
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(44)",
                oldUnicode: false,
                oldMaxLength: 44,
                oldNullable: true);
        }

        /// <inheritdoc />
        /// <remarks>
        /// WARNING: this rollback truncates a credential in the current format (84 characters)
        /// to 44 characters and makes it unusable. Before rolling back, every account that
        /// already signed in with the new format must be given a new credential.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Users",
                type: "varchar(44)",
                unicode: false,
                maxLength: 44,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldUnicode: false,
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
