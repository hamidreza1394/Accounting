using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Template.Tests.TenantValidation;

// S3-0g gate G3 (PR-D): sample structure and hand-written migrations that exist ONLY in the test
// assembly. They mimic the shape of the tenant-owned tables (legacy "TenantId" column, alternate key
// (TenantId, Id), unique and plain indexes, a composite foreign key that references the alternate key)
// but are NOT an application migration: the application model and its migrations are untouched.

public sealed class TenantValidationDbContext(DbContextOptions<TenantValidationDbContext> options) : DbContext(options);

[DbContext(typeof(TenantValidationDbContext))]
[Migration("20271202000001_Tv1CreateStructure")]
public sealed class Tv1CreateStructure : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "TvOwners",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
				Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
			},
			constraints: table => table.PrimaryKey("PK_TvOwners", x => x.Id));

		// D1/D2: legacy TenantId (holds the owner id), alternate key, unique + plain index, dependent FK.
		migrationBuilder.CreateTable(
			name: "TvParents",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
				TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
				Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
				Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_TvParents", x => x.Id);
				table.UniqueConstraint("AK_TvParents_TenantId_Id", x => new { x.TenantId, x.Id });
				table.ForeignKey("FK_TvParents_TvOwners_TenantId", x => x.TenantId, "TvOwners", "Id", onDelete: ReferentialAction.Restrict);
			});
		// Written as SQL on purpose: a hand-written CreateIndex(unique) gets an automatic "[x] IS NOT NULL" filter
		// (EF has no model to see that the columns are NOT NULL), and a filter that names the column blocks
		// sp_rename (see D1b). A unique index over NOT NULL columns generated from a real model has no filter.
		migrationBuilder.Sql("CREATE UNIQUE INDEX [UX_TvParents_TenantId_Code] ON [TvParents] ([TenantId], [Code]);");
		migrationBuilder.CreateIndex(name: "IX_TvParents_TenantId", table: "TvParents", column: "TenantId");

		migrationBuilder.CreateTable(
			name: "TvChildren",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
				TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
				ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
				Note = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_TvChildren", x => x.Id);
				table.ForeignKey(
					"FK_TvChildren_TvParents_TenantId_ParentId",
					x => new { x.TenantId, x.ParentId },
					"TvParents",
					new[] { "TenantId", "Id" },
					onDelete: ReferentialAction.Restrict);
			});
		migrationBuilder.CreateIndex(name: "IX_TvChildren_TenantId_ParentId", table: "TvChildren", columns: new[] { "TenantId", "ParentId" });

		// D3: composite FK (CompanyId, TenantId) over an alternate key whose TenantId column is nullable.
		migrationBuilder.CreateTable(
			name: "TvD3Companies",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
				TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
				Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_TvD3Companies", x => x.Id);
				table.UniqueConstraint("AK_TvD3Companies_Id_TenantId", x => new { x.Id, x.TenantId });
			});
		migrationBuilder.CreateTable(
			name: "TvD3Children",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
				CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
				TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
				Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_TvD3Children", x => x.Id);
				table.ForeignKey(
					"FK_TvD3Children_TvD3Companies_CompanyId_TenantId",
					x => new { x.CompanyId, x.TenantId },
					"TvD3Companies",
					new[] { "Id", "TenantId" },
					onDelete: ReferentialAction.Restrict);
			});

		// D4: nullable Guid column with a dependent foreign key and a dependent index.
		migrationBuilder.CreateTable(
			name: "TvD4Items",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
				Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
				TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_TvD4Items", x => x.Id);
				table.ForeignKey("FK_TvD4Items_TvOwners_TenantId", x => x.TenantId, "TvOwners", "Id", onDelete: ReferentialAction.Restrict);
			});
		migrationBuilder.CreateIndex(name: "IX_TvD4Items_TenantId", table: "TvD4Items", column: "TenantId");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "TvD4Items");
		migrationBuilder.DropTable(name: "TvD3Children");
		migrationBuilder.DropTable(name: "TvD3Companies");
		migrationBuilder.DropTable(name: "TvChildren");
		migrationBuilder.DropTable(name: "TvParents");
		migrationBuilder.DropTable(name: "TvOwners");
	}
}

/// <summary>Pinned rename: columns and the two plain/unique index names move; AK and FK names are kept.</summary>
[DbContext(typeof(TenantValidationDbContext))]
[Migration("20271202000002_Tv2RenamePinned")]
public sealed class Tv2RenamePinned : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.RenameColumn(name: "TenantId", table: "TvParents", newName: "CompanyId");
		migrationBuilder.RenameColumn(name: "TenantId", table: "TvChildren", newName: "CompanyId");
		migrationBuilder.RenameIndex(name: "UX_TvParents_TenantId_Code", table: "TvParents", newName: "UX_TvParents_CompanyId_Code");
		migrationBuilder.RenameIndex(name: "IX_TvParents_TenantId", table: "TvParents", newName: "IX_TvParents_CompanyId");
		migrationBuilder.RenameIndex(name: "IX_TvChildren_TenantId_ParentId", table: "TvChildren", newName: "IX_TvChildren_CompanyId_ParentId");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.RenameIndex(name: "IX_TvChildren_CompanyId_ParentId", table: "TvChildren", newName: "IX_TvChildren_TenantId_ParentId");
		migrationBuilder.RenameIndex(name: "IX_TvParents_CompanyId", table: "TvParents", newName: "IX_TvParents_TenantId");
		migrationBuilder.RenameIndex(name: "UX_TvParents_CompanyId_Code", table: "TvParents", newName: "UX_TvParents_TenantId_Code");
		migrationBuilder.RenameColumn(name: "CompanyId", table: "TvChildren", newName: "TenantId");
		migrationBuilder.RenameColumn(name: "CompanyId", table: "TvParents", newName: "TenantId");
	}
}

/// <summary>
/// Hand-written NOT NULL step (D4): a guard that THROWs while NULL rows exist, then the column change.
/// No default value, no UPDATE: filling the data is a separate, reviewed step that must happen first.
/// </summary>
[DbContext(typeof(TenantValidationDbContext))]
[Migration("20271202000003_Tv3NotNullGuid")]
public sealed class Tv3NotNullGuid : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [TvD4Items] WHERE [TenantId] IS NULL) THROW 50001, N'TvD4Items.TenantId still has NULL rows; backfill first.', 1;");
		// SQL Server refuses to change the nullability of a column an index depends on (error 5074, see D4):
		// the dependent index is dropped and created again explicitly, around the column change.
		migrationBuilder.DropIndex(name: "IX_TvD4Items_TenantId", table: "TvD4Items");
		migrationBuilder.AlterColumn<Guid>(
			name: "TenantId",
			table: "TvD4Items",
			type: "uniqueidentifier",
			nullable: false,
			oldClrType: typeof(Guid),
			oldType: "uniqueidentifier",
			oldNullable: true);
		migrationBuilder.CreateIndex(name: "IX_TvD4Items_TenantId", table: "TvD4Items", column: "TenantId");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(name: "IX_TvD4Items_TenantId", table: "TvD4Items");
		migrationBuilder.AlterColumn<Guid>(
			name: "TenantId",
			table: "TvD4Items",
			type: "uniqueidentifier",
			nullable: true,
			oldClrType: typeof(Guid),
			oldType: "uniqueidentifier");
		migrationBuilder.CreateIndex(name: "IX_TvD4Items_TenantId", table: "TvD4Items", column: "TenantId");
	}
}
