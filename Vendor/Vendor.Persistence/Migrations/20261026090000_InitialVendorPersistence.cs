using Faranou.Vendor.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Faranou.Vendor.Persistence.Migrations;

[DbContext(typeof(VendorDbContext))]
[Migration("20261026090000_InitialVendorPersistence")]
public sealed class InitialVendorPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "vendor");

        migrationBuilder.CreateTable(
            name: "AdminRoles",
            schema: "vendor",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AdminRoles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AdminUsers",
            schema: "vendor",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DisabledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                AccessFailedCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AdminUsers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AuditEvents",
            schema: "vendor",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Action = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                TargetType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                TargetId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PreviousHash = table.Column<byte[]>(type: "varbinary(32)", nullable: false),
                EventHash = table.Column<byte[]>(type: "varbinary(32)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AuditEvents", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AdminRoleClaims",
            schema: "vendor",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdminRoleClaims", x => x.Id);
                table.ForeignKey(name: "FK_AdminRoleClaims_AdminRoles_RoleId", column: x => x.RoleId, principalSchema: "vendor", principalTable: "AdminRoles", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AdminUserClaims",
            schema: "vendor",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdminUserClaims", x => x.Id);
                table.ForeignKey(name: "FK_AdminUserClaims_AdminUsers_UserId", column: x => x.UserId, principalSchema: "vendor", principalTable: "AdminUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AdminUserLogins",
            schema: "vendor",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ProviderKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdminUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(name: "FK_AdminUserLogins_AdminUsers_UserId", column: x => x.UserId, principalSchema: "vendor", principalTable: "AdminUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AdminUserRoles",
            schema: "vendor",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdminUserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(name: "FK_AdminUserRoles_AdminRoles_RoleId", column: x => x.RoleId, principalSchema: "vendor", principalTable: "AdminRoles", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey(name: "FK_AdminUserRoles_AdminUsers_UserId", column: x => x.UserId, principalSchema: "vendor", principalTable: "AdminUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AdminUserTokens",
            schema: "vendor",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LoginProvider = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdminUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(name: "FK_AdminUserTokens_AdminUsers_UserId", column: x => x.UserId, principalSchema: "vendor", principalTable: "AdminUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("RoleNameIndex", "AdminRoles", "NormalizedName", schema: "vendor", unique: true, filter: "[NormalizedName] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_AdminRoleClaims_RoleId", "AdminRoleClaims", "RoleId", schema: "vendor");
        migrationBuilder.CreateIndex("IX_AdminUserClaims_UserId", "AdminUserClaims", "UserId", schema: "vendor");
        migrationBuilder.CreateIndex("IX_AdminUserLogins_UserId", "AdminUserLogins", "UserId", schema: "vendor");
        migrationBuilder.CreateIndex("IX_AdminUserRoles_RoleId", "AdminUserRoles", "RoleId", schema: "vendor");
        migrationBuilder.CreateIndex("EmailIndex", "AdminUsers", "NormalizedEmail", schema: "vendor");
        migrationBuilder.CreateIndex("UserNameIndex", "AdminUsers", "NormalizedUserName", schema: "vendor", unique: true, filter: "[NormalizedUserName] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_AuditEvents_CorrelationId", "AuditEvents", "CorrelationId", schema: "vendor");
        migrationBuilder.CreateIndex("IX_AuditEvents_OccurredAtUtc", "AuditEvents", "OccurredAtUtc", schema: "vendor");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AdminRoleClaims", schema: "vendor");
        migrationBuilder.DropTable(name: "AdminUserClaims", schema: "vendor");
        migrationBuilder.DropTable(name: "AdminUserLogins", schema: "vendor");
        migrationBuilder.DropTable(name: "AdminUserRoles", schema: "vendor");
        migrationBuilder.DropTable(name: "AdminUserTokens", schema: "vendor");
        migrationBuilder.DropTable(name: "AuditEvents", schema: "vendor");
        migrationBuilder.DropTable(name: "AdminRoles", schema: "vendor");
        migrationBuilder.DropTable(name: "AdminUsers", schema: "vendor");
    }
}
