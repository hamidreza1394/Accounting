using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignSchemaWithModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [PostCategory] SET [Title] = N'' WHERE [Title] IS NULL;");

            migrationBuilder.DropIndex(
                name: "IX_PostCategory_ParentId_Title",
                table: "PostCategory");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "PostCategory",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostCategory_ParentId_Title",
                table: "PostCategory",
                columns: new[] { "ParentId", "Title" },
                unique: true,
                filter: "[ParentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostCategory_ParentId_Title",
                table: "PostCategory");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "PostCategory",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_PostCategory_ParentId_Title",
                table: "PostCategory",
                columns: new[] { "ParentId", "Title" },
                unique: true,
                filter: "[ParentId] IS NOT NULL AND [Title] IS NOT NULL");
        }
    }
}
