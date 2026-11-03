using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedCompanyUserListPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'users.list') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1103-4d00-8000-000000000002', 'users.list', N'مشاهده کاربران شرکت', N'اجازه مشاهده فهرست کاربران دارای عضویت در همین شرکت؛ بدون دسترسی به سایر شرکت‌ها یا اطلاعات امنیتی.', 21001, SYSDATETIME());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [PermissionDefinitions] WHERE [Id] = 'f47a2026-1103-4d00-8000-000000000002' AND [Code] = 'users.list' AND NOT EXISTS (SELECT 1 FROM [CompanyRolePermissions] WHERE [CompanyRolePermissions].[PermissionId] = [PermissionDefinitions].[Id]);");
        }
    }
}
