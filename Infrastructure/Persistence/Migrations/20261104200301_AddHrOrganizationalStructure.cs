using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHrOrganizationalStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_UserCompanyMemberships_Id_TenantId",
                table: "UserCompanyMemberships",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.CreateTable(
                name: "CompanyEmployees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmploymentStatus = table.Column<int>(type: "int", nullable: false),
                    HiredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerminatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserCompanyMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyEmployees", x => x.Id);
                    table.UniqueConstraint("AK_CompanyEmployees_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CompanyEmployees_Companies_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyEmployees_UserCompanyMemberships_UserCompanyMembershipId_TenantId",
                        columns: x => new { x.UserCompanyMembershipId, x.TenantId },
                        principalTable: "UserCompanyMemberships",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyOrganizationalUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ParentUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyOrganizationalUnits", x => x.Id);
                    table.UniqueConstraint("AK_CompanyOrganizationalUnits_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CompanyOrganizationalUnits_Companies_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyOrganizationalUnits_CompanyOrganizationalUnits_ParentUnitId_TenantId",
                        columns: x => new { x.ParentUnitId, x.TenantId },
                        principalTable: "CompanyOrganizationalUnits",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPositions", x => x.Id);
                    table.UniqueConstraint("AK_CompanyPositions_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_CompanyPositions_Companies_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyEmployeeAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationalUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyEmployeeAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyEmployeeAssignments_Companies_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyEmployeeAssignments_CompanyEmployees_EmployeeId_TenantId",
                        columns: x => new { x.EmployeeId, x.TenantId },
                        principalTable: "CompanyEmployees",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyEmployeeAssignments_CompanyEmployees_ManagerEmployeeId_TenantId",
                        columns: x => new { x.ManagerEmployeeId, x.TenantId },
                        principalTable: "CompanyEmployees",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyEmployeeAssignments_CompanyOrganizationalUnits_OrganizationalUnitId_TenantId",
                        columns: x => new { x.OrganizationalUnitId, x.TenantId },
                        principalTable: "CompanyOrganizationalUnits",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyEmployeeAssignments_CompanyPositions_PositionId_TenantId",
                        columns: x => new { x.PositionId, x.TenantId },
                        principalTable: "CompanyPositions",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeAssignments_EmployeeId_TenantId",
                table: "CompanyEmployeeAssignments",
                columns: new[] { "EmployeeId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeAssignments_ManagerEmployeeId_TenantId",
                table: "CompanyEmployeeAssignments",
                columns: new[] { "ManagerEmployeeId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeAssignments_OrganizationalUnitId_TenantId",
                table: "CompanyEmployeeAssignments",
                columns: new[] { "OrganizationalUnitId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeAssignments_PositionId_TenantId",
                table: "CompanyEmployeeAssignments",
                columns: new[] { "PositionId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeAssignments_TenantId_EmployeeId",
                table: "CompanyEmployeeAssignments",
                columns: new[] { "TenantId", "EmployeeId" },
                unique: true,
                filter: "[Status] = 1 AND [EndOnUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeAssignments_TenantId_OrganizationalUnitId",
                table: "CompanyEmployeeAssignments",
                columns: new[] { "TenantId", "OrganizationalUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeAssignments_TenantId_PositionId",
                table: "CompanyEmployeeAssignments",
                columns: new[] { "TenantId", "PositionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployees_TenantId_EmployeeNumber",
                table: "CompanyEmployees",
                columns: new[] { "TenantId", "EmployeeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployees_UserCompanyMembershipId_TenantId",
                table: "CompanyEmployees",
                columns: new[] { "UserCompanyMembershipId", "TenantId" },
                unique: true,
                filter: "[UserCompanyMembershipId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyOrganizationalUnits_ParentUnitId_TenantId",
                table: "CompanyOrganizationalUnits",
                columns: new[] { "ParentUnitId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyOrganizationalUnits_TenantId_Code",
                table: "CompanyOrganizationalUnits",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyOrganizationalUnits_TenantId_ParentUnitId",
                table: "CompanyOrganizationalUnits",
                columns: new[] { "TenantId", "ParentUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPositions_TenantId_Code",
                table: "CompanyPositions",
                columns: new[] { "TenantId", "Code" },
                unique: true);
            migrationBuilder.Sql("ALTER TABLE [CompanyOrganizationalUnits] ADD CONSTRAINT [CK_CompanyOrganizationalUnits_Period] CHECK ([ValidToUtc] IS NULL OR [ValidFromUtc] IS NULL OR [ValidToUtc] >= [ValidFromUtc]);");
            migrationBuilder.Sql("ALTER TABLE [CompanyEmployees] ADD CONSTRAINT [CK_CompanyEmployees_EmploymentStatus] CHECK ([EmploymentStatus] BETWEEN 1 AND 3);");
            migrationBuilder.Sql("ALTER TABLE [CompanyEmployees] ADD CONSTRAINT [CK_CompanyEmployees_Termination] CHECK (([EmploymentStatus] = 3 AND [TerminatedOnUtc] IS NOT NULL) OR ([EmploymentStatus] <> 3 AND [TerminatedOnUtc] IS NULL));");
            migrationBuilder.Sql("ALTER TABLE [CompanyEmployeeAssignments] ADD CONSTRAINT [CK_CompanyEmployeeAssignments_Status] CHECK ([Status] BETWEEN 1 AND 2);");
            migrationBuilder.Sql("ALTER TABLE [CompanyEmployeeAssignments] ADD CONSTRAINT [CK_CompanyEmployeeAssignments_Period] CHECK ([EndOnUtc] IS NULL OR [EndOnUtc] >= [StartOnUtc]);");
            migrationBuilder.Sql("ALTER TABLE [CompanyEmployeeAssignments] ADD CONSTRAINT [CK_CompanyEmployeeAssignments_SelfManager] CHECK ([ManagerEmployeeId] IS NULL OR [ManagerEmployeeId] <> [EmployeeId]);");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.employees.list') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000001', 'hr.employees.list', N'فهرست کارکنان شرکت', N'اجازه مشاهده فهرست کارکنان همان شرکت؛ بدون داده تماس، نقش، مجوز یا داده امنیتی کاربران.', 22001, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.employees.view') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000002', 'hr.employees.view', N'مشاهده جزئیات کارمند', N'اجازه مشاهده جزئیات یک کارمند و انتساب سازمانی او در همان شرکت.', 22002, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.employees.create') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000003', 'hr.employees.create', N'ثبت کارمند جدید', N'اجازه ثبت رکورد پرسنلی جدید در همان شرکت؛ کارمند می‌تواند بدون حساب ورود ثبت شود و هیچ مجوزی ایجاد نمی‌کند.', 22003, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.employees.update') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000004', 'hr.employees.update', N'ویرایش و خاتمه همکاری کارمند', N'اجازه ویرایش اطلاعات پایه و وضعیت اشتغال کارمندان همان شرکت؛ خاتمه همکاری عضویت یا نشست کاربر را ابطال نمی‌کند.', 22004, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.employees.user-link.manage') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000005', 'hr.employees.user-link.manage', N'اتصال کارمند به حساب کاربری', N'اجازه اتصال یا جداسازی کارمند به عضویت فعال همان شرکت؛ این اتصال هیچ مجوز یا عضویت جدیدی ایجاد نمی‌کند.', 22005, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.organizational-units.list') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000006', 'hr.organizational-units.list', N'مشاهده واحدهای سازمانی', N'اجازه مشاهده فهرست و درخت واحدهای سازمانی همان شرکت.', 22006, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.organizational-units.create') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000007', 'hr.organizational-units.create', N'ایجاد واحد سازمانی', N'اجازه ایجاد واحد سازمانی در همان شرکت؛ واحد والد باید متعلق به همان شرکت باشد.', 22007, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.organizational-units.update') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000008', 'hr.organizational-units.update', N'ویرایش و غیرفعال‌سازی واحد سازمانی', N'اجازه ویرایش یا غیرفعال‌سازی واحدهای همان شرکت؛ واحد دارای فرزند یا انتساب فعال غیرفعال نمی‌شود.', 22008, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.positions.list') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000009', 'hr.positions.list', N'مشاهده سمت‌های سازمانی', N'اجازه مشاهده فهرست سمت‌های سازمانی همان شرکت.', 22009, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.positions.create') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000010', 'hr.positions.create', N'ایجاد سمت سازمانی', N'اجازه ایجاد سمت سازمانی در همان شرکت؛ سمت هیچ مجوزی ایجاد نمی‌کند.', 22010, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.positions.update') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000011', 'hr.positions.update', N'ویرایش و غیرفعال‌سازی سمت سازمانی', N'اجازه ویرایش یا غیرفعال‌سازی سمت‌های همان شرکت؛ سمت دارای انتساب فعال غیرفعال نمی‌شود.', 22011, SYSDATETIME());");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'hr.assignments.manage') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('f47a2026-1105-4d00-8000-000000000012', 'hr.assignments.manage', N'مدیریت انتساب سازمانی', N'اجازه شروع، پایان و تغییر انتساب کارمندان به واحد، سمت و مدیر در همان شرکت؛ انتساب هیچ مجوز عملیاتی ایجاد نمی‌کند.', 22012, SYSDATETIME());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql("DELETE FROM [PermissionDefinitions] WHERE [Id] IN ('f47a2026-1105-4d00-8000-000000000001', 'f47a2026-1105-4d00-8000-000000000002', 'f47a2026-1105-4d00-8000-000000000003', 'f47a2026-1105-4d00-8000-000000000004', 'f47a2026-1105-4d00-8000-000000000005', 'f47a2026-1105-4d00-8000-000000000006', 'f47a2026-1105-4d00-8000-000000000007', 'f47a2026-1105-4d00-8000-000000000008', 'f47a2026-1105-4d00-8000-000000000009', 'f47a2026-1105-4d00-8000-000000000010', 'f47a2026-1105-4d00-8000-000000000011', 'f47a2026-1105-4d00-8000-000000000012') AND NOT EXISTS (SELECT 1 FROM [CompanyRolePermissions] WHERE [CompanyRolePermissions].[PermissionId] = [PermissionDefinitions].[Id]);");
            migrationBuilder.DropTable(
                name: "CompanyEmployeeAssignments");

            migrationBuilder.DropTable(
                name: "CompanyEmployees");

            migrationBuilder.DropTable(
                name: "CompanyOrganizationalUnits");

            migrationBuilder.DropTable(
                name: "CompanyPositions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_UserCompanyMemberships_Id_TenantId",
                table: "UserCompanyMemberships");
        }
    }
}
