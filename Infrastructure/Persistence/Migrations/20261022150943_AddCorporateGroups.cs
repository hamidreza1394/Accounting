using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_FiscalPeriods_Id_TenantId",
                table: "FiscalPeriods",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.CreateTable(
                name: "CorporateGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateGroups_Companies_OwnerCompanyId",
                        column: x => x.OwnerCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OwnershipRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubsidiaryCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnershipPercentage = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    VotingPercentage = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    ControlBasis = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "Date", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "Date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnershipRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OwnershipRelationships_Companies_ParentCompanyId",
                        column: x => x.ParentCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OwnershipRelationships_Companies_SubsidiaryCompanyId",
                        column: x => x.SubsidiaryCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsolidationProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorporateGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AccountingFramework = table.Column<int>(type: "int", nullable: false),
                    ReportingCurrencyCode = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    FiscalPeriodMappingPolicy = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsolidationProfiles", x => x.Id);
                    table.UniqueConstraint("AK_ConsolidationProfiles_Id_CorporateGroupId", x => new { x.Id, x.CorporateGroupId });
                    table.ForeignKey(
                        name: "FK_ConsolidationProfiles_CorporateGroups_CorporateGroupId",
                        column: x => x.CorporateGroupId,
                        principalTable: "CorporateGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorporateGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorporateGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberRole = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "Date", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "Date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateGroupMembers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorporateGroupMembers_CorporateGroups_CorporateGroupId",
                        column: x => x.CorporateGroupId,
                        principalTable: "CorporateGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsolidationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorporateGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsolidationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "Date", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "Date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FailureCode = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordering = table.Column<int>(type: "int", nullable: false),
                    InsertDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsolidationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsolidationRuns_ConsolidationProfiles_ConsolidationProfileId_CorporateGroupId",
                        columns: x => new { x.ConsolidationProfileId, x.CorporateGroupId },
                        principalTable: "ConsolidationProfiles",
                        principalColumns: new[] { "Id", "CorporateGroupId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsolidationRuns_CorporateGroups_CorporateGroupId",
                        column: x => x.CorporateGroupId,
                        principalTable: "CorporateGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsolidationRunCompanies",
                columns: table => new
                {
                    ConsolidationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberRole = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsolidationRunCompanies", x => new { x.ConsolidationRunId, x.CompanyId });
                    table.ForeignKey(
                        name: "FK_ConsolidationRunCompanies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsolidationRunCompanies_ConsolidationRuns_ConsolidationRunId",
                        column: x => x.ConsolidationRunId,
                        principalTable: "ConsolidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsolidationRunCompanies_FiscalPeriods_FiscalPeriodId_CompanyId",
                        columns: x => new { x.FiscalPeriodId, x.CompanyId },
                        principalTable: "FiscalPeriods",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsolidationProfiles_CorporateGroupId_Name",
                table: "ConsolidationProfiles",
                columns: new[] { "CorporateGroupId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsolidationRunCompanies_CompanyId",
                table: "ConsolidationRunCompanies",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsolidationRunCompanies_FiscalPeriodId_CompanyId",
                table: "ConsolidationRunCompanies",
                columns: new[] { "FiscalPeriodId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsolidationRuns_ConsolidationProfileId_CorporateGroupId",
                table: "ConsolidationRuns",
                columns: new[] { "ConsolidationProfileId", "CorporateGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsolidationRuns_CorporateGroupId_StartedAtUtc",
                table: "ConsolidationRuns",
                columns: new[] { "CorporateGroupId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateGroupMembers_CompanyId",
                table: "CorporateGroupMembers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateGroupMembers_CorporateGroupId_CompanyId_ValidFrom",
                table: "CorporateGroupMembers",
                columns: new[] { "CorporateGroupId", "CompanyId", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateGroups_OwnerCompanyId_Code",
                table: "CorporateGroups",
                columns: new[] { "OwnerCompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateGroups_OwnerCompanyId_IsActive",
                table: "CorporateGroups",
                columns: new[] { "OwnerCompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipRelationships_ControlBasis",
                table: "OwnershipRelationships",
                column: "ControlBasis");

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipRelationships_ParentCompanyId_SubsidiaryCompanyId_ValidFrom",
                table: "OwnershipRelationships",
                columns: new[] { "ParentCompanyId", "SubsidiaryCompanyId", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OwnershipRelationships_SubsidiaryCompanyId",
                table: "OwnershipRelationships",
                column: "SubsidiaryCompanyId");

            // The CHECK constraints and the permission catalog rows are not part of the EF model;
            // they are written by hand (scripts/generate_migrations.py, ADD_CORPORATE_GROUPS_EPILOGUE).
            migrationBuilder.Sql("ALTER TABLE [CorporateGroupMembers] ADD CONSTRAINT [CK_CorporateGroupMembers_ValidPeriod] CHECK ([ValidTo] IS NULL OR [ValidTo] >= [ValidFrom]);");

            migrationBuilder.Sql("ALTER TABLE [CorporateGroupMembers] ADD CONSTRAINT [CK_CorporateGroupMembers_MemberRole] CHECK ([MemberRole] BETWEEN 1 AND 5);");

            migrationBuilder.Sql("ALTER TABLE [OwnershipRelationships] ADD CONSTRAINT [CK_OwnershipRelationships_DistinctCompanies] CHECK ([ParentCompanyId] <> [SubsidiaryCompanyId]);");

            migrationBuilder.Sql("ALTER TABLE [OwnershipRelationships] ADD CONSTRAINT [CK_OwnershipRelationships_OwnershipPercentage] CHECK ([OwnershipPercentage] >= 0 AND [OwnershipPercentage] <= 100);");

            migrationBuilder.Sql("ALTER TABLE [OwnershipRelationships] ADD CONSTRAINT [CK_OwnershipRelationships_VotingPercentage] CHECK ([VotingPercentage] >= 0 AND [VotingPercentage] <= 100);");

            migrationBuilder.Sql("ALTER TABLE [OwnershipRelationships] ADD CONSTRAINT [CK_OwnershipRelationships_ControlBasis] CHECK ([ControlBasis] BETWEEN 1 AND 4);");

            migrationBuilder.Sql("ALTER TABLE [OwnershipRelationships] ADD CONSTRAINT [CK_OwnershipRelationships_ValidPeriod] CHECK ([ValidTo] IS NULL OR [ValidTo] >= [ValidFrom]);");

            migrationBuilder.Sql("ALTER TABLE [ConsolidationProfiles] ADD CONSTRAINT [CK_ConsolidationProfiles_ReportingCurrencyCode] CHECK (LEN([ReportingCurrencyCode]) = 3 AND [ReportingCurrencyCode] COLLATE Latin1_General_BIN NOT LIKE '%[^A-Z]%');");

            migrationBuilder.Sql("ALTER TABLE [ConsolidationRuns] ADD CONSTRAINT [CK_ConsolidationRuns_Period] CHECK ([PeriodEnd] >= [PeriodStart]);");

            migrationBuilder.Sql("ALTER TABLE [ConsolidationRuns] ADD CONSTRAINT [CK_ConsolidationRuns_ProfileOfConsolidated] CHECK ([Kind] <> 2 OR [ConsolidationProfileId] IS NOT NULL);");

            migrationBuilder.Sql("ALTER TABLE [ConsolidationRuns] ADD CONSTRAINT [CK_ConsolidationRuns_Kind] CHECK ([Kind] BETWEEN 1 AND 2);");

            migrationBuilder.Sql("ALTER TABLE [ConsolidationRuns] ADD CONSTRAINT [CK_ConsolidationRuns_Status] CHECK ([Status] BETWEEN 1 AND 5);");

            migrationBuilder.Sql("ALTER TABLE [ConsolidationProfiles] ADD CONSTRAINT [CK_ConsolidationProfiles_AccountingFramework] CHECK ([AccountingFramework] BETWEEN 1 AND 2);");

            migrationBuilder.Sql("ALTER TABLE [ConsolidationProfiles] ADD CONSTRAINT [CK_ConsolidationProfiles_FiscalPeriodMappingPolicy] CHECK ([FiscalPeriodMappingPolicy] = 1);");

            migrationBuilder.Sql("ALTER TABLE [ConsolidationRunCompanies] ADD CONSTRAINT [CK_ConsolidationRunCompanies_MemberRole] CHECK ([MemberRole] BETWEEN 1 AND 5);");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'corporate-groups.view') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a01', 'corporate-groups.view', N'مشاهده گروه شرکتی', NULL, 10000, SYSDATETIME());");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'corporate-groups.manage') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a02', 'corporate-groups.manage', N'مدیریت گروه شرکتی و پروفایل تلفیق', NULL, 10000, SYSDATETIME());");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'corporate-groups.members.manage') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a03', 'corporate-groups.members.manage', N'مدیریت عضویت شرکت در گروه', NULL, 10000, SYSDATETIME());");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'corporate-groups.ownership.manage') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a04', 'corporate-groups.ownership.manage', N'ثبت روابط مالکیت و کنترل', NULL, 10000, SYSDATETIME());");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'reporting.group-aggregate.view') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a05', 'reporting.group-aggregate.view', N'اجرای گزارش تجمیعی گروه', NULL, 10000, SYSDATETIME());");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'reporting.group-consolidated.run') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a06', 'reporting.group-consolidated.run', N'اجرای گزارش تلفیقی گروه', NULL, 10000, SYSDATETIME());");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'consolidation.adjustments.approve') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a07', 'consolidation.adjustments.approve', N'تأیید تعدیلات تلفیقی', NULL, 10000, SYSDATETIME());");

            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM [PermissionDefinitions] WHERE [Code] = 'reporting.group.export') INSERT INTO [PermissionDefinitions] ([Id], [Code], [Name], [Description], [Ordering], [InsertDateTime]) VALUES ('0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a08', 'reporting.group.export', N'دریافت خروجی گزارش گروه', NULL, 10000, SYSDATETIME());");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the catalog rows that no company role uses are removed.
            migrationBuilder.Sql("DELETE FROM [PermissionDefinitions] WHERE [Id] IN ('0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a01', '0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a02', '0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a03', '0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a04', '0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a05', '0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a06', '0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a07', '0b6d2f3e-5d0a-4c1e-9a51-2c7c0e3f1a08') AND NOT EXISTS (SELECT 1 FROM [CompanyRolePermissions] WHERE [CompanyRolePermissions].[PermissionId] = [PermissionDefinitions].[Id]);");

            migrationBuilder.DropTable(
                name: "ConsolidationRunCompanies");

            migrationBuilder.DropTable(
                name: "CorporateGroupMembers");

            migrationBuilder.DropTable(
                name: "OwnershipRelationships");

            migrationBuilder.DropTable(
                name: "ConsolidationRuns");

            migrationBuilder.DropTable(
                name: "ConsolidationProfiles");

            migrationBuilder.DropTable(
                name: "CorporateGroups");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FiscalPeriods_Id_TenantId",
                table: "FiscalPeriods");
        }
    }
}
