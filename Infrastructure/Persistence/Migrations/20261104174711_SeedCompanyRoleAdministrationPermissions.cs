using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedCompanyRoleAdministrationPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'roles.view') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1104-4d00-8000-000000000001', 'roles.view', N'مشاهده گروه‌های دسترسی شرکت', N'اجازه مشاهده گروه‌های دسترسی، مجوزها و اعضای همان شرکت؛ بدون دسترسی به شرکت‌های دیگر.', 21002, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'roles.create') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1104-4d00-8000-000000000002', 'roles.create', N'ایجاد گروه دسترسی در شرکت', N'اجازه ایجاد گروه دسترسی جدید در همان شرکت؛ فقط با مجوزهایی که خود کاربر حق واگذاری آن‌ها را دارد (سقف تفویض).', 21003, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'roles.update') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1104-4d00-8000-000000000003', 'roles.update', N'ویرایش گروه دسترسی شرکت', N'اجازه ویرایش عنوان، وضعیت و مجوزهای گروه‌های دسترسی همان شرکت؛ فقط با مجوزهایی که خود کاربر حق واگذاری آن‌ها را دارد (سقف تفویض).', 21004, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'roles.assign') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1104-4d00-8000-000000000004', 'roles.assign', N'تخصیص نقش به اعضای شرکت', N'اجازه تخصیص و انتقال نقش اعضای فعال همان شرکت؛ فقط با گروه‌هایی که مجوزهای آن‌ها در سقف تفویض کاربر است.', 21005, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'corporate-groups.directory.view') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1104-4d00-8000-000000000005', 'corporate-groups.directory.view', N'مشاهده دفترچه کاربران هم‌گروه', N'اجازه مشاهده فهرست فقط-خواندنی و محدود کاربران شرکت‌های عضو همان گروه شرکتی؛ بدون اطلاعات تماس، نقش، مجوز یا داده امنیتی.', 21006, SYSDATETIME());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [PermissionDefinitions] WHERE [Id] = 'f47a2026-1104-4d00-8000-000000000001' AND [Code] = 'roles.view' AND NOT EXISTS (SELECT 1 FROM [CompanyRolePermissions] WHERE [CompanyRolePermissions].[PermissionId] = [PermissionDefinitions].[Id]);");
            migrationBuilder.Sql("DELETE FROM [PermissionDefinitions] WHERE [Id] = 'f47a2026-1104-4d00-8000-000000000002' AND [Code] = 'roles.create' AND NOT EXISTS (SELECT 1 FROM [CompanyRolePermissions] WHERE [CompanyRolePermissions].[PermissionId] = [PermissionDefinitions].[Id]);");
            migrationBuilder.Sql("DELETE FROM [PermissionDefinitions] WHERE [Id] = 'f47a2026-1104-4d00-8000-000000000003' AND [Code] = 'roles.update' AND NOT EXISTS (SELECT 1 FROM [CompanyRolePermissions] WHERE [CompanyRolePermissions].[PermissionId] = [PermissionDefinitions].[Id]);");
            migrationBuilder.Sql("DELETE FROM [PermissionDefinitions] WHERE [Id] = 'f47a2026-1104-4d00-8000-000000000004' AND [Code] = 'roles.assign' AND NOT EXISTS (SELECT 1 FROM [CompanyRolePermissions] WHERE [CompanyRolePermissions].[PermissionId] = [PermissionDefinitions].[Id]);");
            migrationBuilder.Sql("DELETE FROM [PermissionDefinitions] WHERE [Id] = 'f47a2026-1104-4d00-8000-000000000005' AND [Code] = 'corporate-groups.directory.view' AND NOT EXISTS (SELECT 1 FROM [CompanyRolePermissions] WHERE [CompanyRolePermissions].[PermissionId] = [PermissionDefinitions].[Id]);");
        }
    }
}
