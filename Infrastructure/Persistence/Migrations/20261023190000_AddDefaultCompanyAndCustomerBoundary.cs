using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultCompanyAndCustomerBoundary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultCompanyId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerBoundaryId",
                table: "Companies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_CustomerBoundaryId",
                table: "Companies",
                column: "CustomerBoundaryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Companies_CustomerBoundaryId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CustomerBoundaryId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "DefaultCompanyId",
                table: "Users");
        }
    }
}
