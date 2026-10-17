using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Data;

/// <summary>
/// Design-time factory used by the EF Core tools (<c>dotnet ef</c>).
/// </summary>
/// <remarks>
/// <para>
/// The server (<c>Server/Program.cs</c>) is a Razor Pages host that refuses to start without a
/// valid connection string, therefore the tools cannot rely on the application host to build the
/// model. This factory builds the <see cref="DatabaseContext" /> directly, so
/// <c>dotnet ef migrations add</c> / <c>dotnet ef database update</c> work without booting the web
/// application and without a reachable database server.
/// </para>
/// <para>
/// The connection string is only used to pick the provider and the migrations assembly; the
/// design-time operations that create migrations never open a connection. For commands that do
/// connect (<c>database update</c>, <c>database update --script</c>) the value is read, in order,
/// from the <c>TARAZAVA_CONNECTIONSTRING</c> environment variable, from the
/// <c>ConnectionStrings:ConnectionString</c> user secret / environment variable of the
/// <c>Server</c> project, and finally from the local development default below.
/// </para>
/// </remarks>
public sealed class DesignTimeDatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
	/// <summary>
	/// Name of the environment variable that overrides the design-time connection string.
	/// </summary>
	public const string ConnectionStringEnvironmentVariableName = "TARAZAVA_CONNECTIONSTRING";

	/// <summary>
	/// Local development fallback. It is only a fallback: it is never used by the running
	/// application, which reads <c>ConnectionStrings:ConnectionString</c> from configuration.
	/// </summary>
	private const string LocalDevelopmentConnectionString =
		"Server=(localdb)\\mssqllocaldb;Database=TarazAvaDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

	public DatabaseContext CreateDbContext(string[] args)
	{
		var connectionString =
			Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariableName)
			?? Environment.GetEnvironmentVariable("ConnectionStrings__ConnectionString")
			?? LocalDevelopmentConnectionString;

		var optionsBuilder =
			new DbContextOptionsBuilder<DatabaseContext>();

		optionsBuilder
			.UseSqlServer(
				connectionString: connectionString,
				sqlServerOptionsAction: sqlServer => sqlServer
					.MigrationsAssembly(assemblyName: typeof(DatabaseContext).Assembly.FullName));

		return new DatabaseContext(optionsBuilder.Options);
	}
}
