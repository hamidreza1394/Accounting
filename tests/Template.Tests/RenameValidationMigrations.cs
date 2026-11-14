using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Template.Tests.RenameValidation;

// PR-SS-1: sample structure and hand-written migrations that exist ONLY in the test assembly.
// They are found by EF through the [DbContext]/[Migration] attributes, never by the application.

public sealed class RenameTestDbContext(DbContextOptions<RenameTestDbContext> options) : DbContext(options);

[DbContext(typeof(RenameTestDbContext))]
[Migration("20271201000001_Rt1CreateStructure")]
public sealed class Rt1CreateStructure : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "RenameOwners",
			columns: table => new
			{
				Id = table.Column<int>(type: "int", nullable: false),
				Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
			},
			constraints: table => table.PrimaryKey("PK_RenameOwners", x => x.Id));

		migrationBuilder.CreateTable(
			name: "RenameChildren",
			columns: table => new
			{
				Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
				OwnerId = table.Column<int>(type: "int", nullable: false),
				Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
				Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_RenameChildren", x => x.Id);
				table.UniqueConstraint("AK_RenameChildren_Owner_Code", x => new { x.OwnerId, x.Code });
				table.ForeignKey(
					name: "FK_RenameChildren_RenameOwners_OwnerId",
					column: x => x.OwnerId,
					principalTable: "RenameOwners",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict);
			});

		migrationBuilder.CreateTable(
			name: "RenameGrandChildren",
			columns: table => new
			{
				Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
				ChildOwnerId = table.Column<int>(type: "int", nullable: false),
				ChildCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
				Note = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_RenameGrandChildren", x => x.Id);
				table.ForeignKey(
					name: "FK_RenameGrandChildren_RenameChildren",
					columns: x => new { x.ChildOwnerId, x.ChildCode },
					principalTable: "RenameChildren",
					principalColumns: new[] { "OwnerId", "Code" },
					onDelete: ReferentialAction.Restrict);
			});

		migrationBuilder.CreateIndex(name: "IX_RenameChildren_OwnerId", table: "RenameChildren", column: "OwnerId");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "RenameGrandChildren");
		migrationBuilder.DropTable(name: "RenameChildren");
		migrationBuilder.DropTable(name: "RenameOwners");
	}
}

[DbContext(typeof(RenameTestDbContext))]
[Migration("20271201000002_Rt2RenameColumn")]
public sealed class Rt2RenameColumn : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder) =>
		migrationBuilder.RenameColumn(name: "OwnerId", table: "RenameChildren", newName: "CompanyId");

	protected override void Down(MigrationBuilder migrationBuilder) =>
		migrationBuilder.RenameColumn(name: "CompanyId", table: "RenameChildren", newName: "OwnerId");
}

[DbContext(typeof(RenameTestDbContext))]
[Migration("20271201000003_Rt3RenameIndex")]
public sealed class Rt3RenameIndex : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder) =>
		migrationBuilder.RenameIndex(name: "IX_RenameChildren_OwnerId", table: "RenameChildren", newName: "IX_RenameChildren_CompanyId");

	protected override void Down(MigrationBuilder migrationBuilder) =>
		migrationBuilder.RenameIndex(name: "IX_RenameChildren_CompanyId", table: "RenameChildren", newName: "IX_RenameChildren_OwnerId");
}
