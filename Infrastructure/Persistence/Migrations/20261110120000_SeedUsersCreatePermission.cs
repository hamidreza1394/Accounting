using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <summary>
    /// D-078: company-scoped permission for creating a user account for (or adding an
    /// existing account to) the route company. The seed is data-only and idempotent; no
    /// company role receives the code automatically — it must be granted through the
    /// approved role/permission assignment flow under the D-071 delegation ceiling.
    /// </summary>
    public partial class SeedUsersCreatePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'users.create') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1103-4d00-8000-000000000003', 'users.create', N'ایجاد کاربر برای شرکت', N'اجازه ایجاد حساب کاربری جدید یا افزودن حساب موجود به همین شرکت؛ عضویت همان شرکت فعال می‌شود اما هیچ نقش، مجوز یا دسترسی سال/دوره‌ای تخصیص نمی‌یابد (D-078).', 21007, SYSDATETIME());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [PermissionDefinitions] WHERE [Id] = 'f47a2026-1103-4d00-8000-000000000003' AND [Code] = 'users.create' AND NOT EXISTS (SELECT 1 FROM [CompanyRolePermissions] WHERE [CompanyRolePermissions].[PermissionId] = [PermissionDefinitions].[Id]);");
        }
    }
}
