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
                name: "IX_CompanyRolePermissions_PermissionId",
                table: "CompanyRolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginLogs_UserId",
                table: "LoginLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_ParentId",
                table: "MenuItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_CreatorUserId",
                table: "Pages",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_PageCategoryId",
                table: "Pages",
                column: "PageCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ApplicationHandlerId",
                table: "Permissions",
                column: "ApplicationHandlerId");

            migrationBuilder.CreateIndex(
                name: "IX_PostCategory_ParentId_Title",
                table: "PostCategory",
                columns: new[] { "ParentId", "Title" },
                unique: true,
                filter: "[ParentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyMemberships_RoleId_TenantId",
                table: "UserCompanyMemberships",
                columns: new[] { "RoleId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserCompanyMemberships_RoleId_TenantId",
                table: "UserCompanyMemberships");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_PostCategory_ParentId_Title",
                table: "PostCategory");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_ApplicationHandlerId",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Pages_PageCategoryId",
                table: "Pages");

            migrationBuilder.DropIndex(
                name: "IX_Pages_CreatorUserId",
                table: "Pages");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_ParentId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_LoginLogs_UserId",
                table: "LoginLogs");

            migrationBuilder.DropIndex(
                name: "IX_CompanyRolePermissions_PermissionId",
                table: "CompanyRolePermissions");

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
