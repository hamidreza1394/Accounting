using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDefaultCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultCompanyId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DefaultCompanyId",
                table: "Users",
                column: "DefaultCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Companies_DefaultCompanyId",
                table: "Users",
                column: "DefaultCompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Companies_DefaultCompanyId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_DefaultCompanyId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DefaultCompanyId",
                table: "Users");
        }
    }
}
