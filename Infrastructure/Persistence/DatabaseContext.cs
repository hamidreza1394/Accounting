using Microsoft.EntityFrameworkCore;

namespace Data;

/// <summary>
/// The EF Core <see cref="DbContext" /> of the Taraz Ava accounting application.
/// </summary>
/// <remarks>
/// <para>
/// This context is intentionally free of side effects: it never creates, alters or drops
/// the database schema. Schema management is fully owned by EF Core migrations that live in
/// <c>Infrastructure/Persistence/Migrations</c> and are applied with
/// <c>dotnet ef database update</c> (or the <c>--migrate</c> switch of the server).
/// </para>
/// <para>
/// Do not reintroduce <c>Database.EnsureCreated()</c> here. <c>EnsureCreated()</c> bypasses
/// the migration history table, so it silently produces a schema that is unknown to
/// <c>__EFMigrationsHistory</c> and makes every later <c>dotnet ef database update</c> fail.
/// </para>
/// </remarks>
public class DatabaseContext : Microsoft.EntityFrameworkCore.DbContext
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public DatabaseContext
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		(Microsoft.EntityFrameworkCore.DbContextOptions<DatabaseContext> options) : base(options: options)
	{
		// No schema mutation here on purpose. See the remarks on the class.
	}

	public Microsoft.EntityFrameworkCore.DbSet<Domain.Role> Roles { get; set; }

	public Microsoft.EntityFrameworkCore.DbSet<Domain.User> Users { get; set; }

	public Microsoft.EntityFrameworkCore.DbSet<Domain.Page> Pages { get; set; }

	public Microsoft.EntityFrameworkCore.DbSet<Domain.LoginLog> LoginLogs { get; set; }

	public Microsoft.EntityFrameworkCore.DbSet<Domain.MenuItem> MenuItems { get; set; }

	public Microsoft.EntityFrameworkCore.DbSet<Domain.Permission> Permissions { get; set; }

	public Microsoft.EntityFrameworkCore.DbSet<Domain.PageCategory> PageCategories { get; set; }

	public Microsoft.EntityFrameworkCore.DbSet<Domain.ApplicationHandler> ApplicationHandlers { get; set; }

    public Microsoft.EntityFrameworkCore.DbSet<Domain.Company> Companies { get; set; }
    public Microsoft.EntityFrameworkCore.DbSet<Domain.UserCompanyMembership> UserCompanyMemberships { get; set; }
    public Microsoft.EntityFrameworkCore.DbSet<Domain.FiscalPeriod> FiscalPeriods { get; set; }
    public Microsoft.EntityFrameworkCore.DbSet<Domain.CompanyRole> CompanyRoles { get; set; }
    public Microsoft.EntityFrameworkCore.DbSet<Domain.PermissionDefinition> PermissionDefinitions { get; set; }
    public Microsoft.EntityFrameworkCore.DbSet<Domain.CompanyRolePermission> CompanyRolePermissions { get; set; }
    public Microsoft.EntityFrameworkCore.DbSet<Domain.SecurityAuditEvent> SecurityAuditEvents { get; set; }

	protected override void OnModelCreating
		(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder: modelBuilder);

		modelBuilder.ApplyConfigurationsFromAssembly
			(assembly: typeof(Configurations.RoleConfiguration).Assembly);
	}

	protected override void ConfigureConventions
		(Microsoft.EntityFrameworkCore.ModelConfigurationBuilder builder)
	{
		base.ConfigureConventions(builder: builder);

		builder.Properties<System.DateOnly>()
			.HaveConversion<Conventions.DateTimeConventions.DateOnlyConverter>()
			.HaveColumnType(typeName: nameof(System.DateTime.Date))
			;

		builder.Properties<System.DateOnly?>()
			.HaveConversion<Conventions.DateTimeConventions.NullableDateOnlyConverter>()
			.HaveColumnType(typeName: nameof(System.DateTime.Date))
			;
	}
}
