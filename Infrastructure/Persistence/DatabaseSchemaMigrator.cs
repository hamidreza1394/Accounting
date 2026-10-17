using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Data;

/// <summary>
/// Applies the EF Core migrations of <see cref="DatabaseContext" /> to the target database.
/// </summary>
public interface IDatabaseSchemaMigrator
{
	/// <summary>
	/// Gets the migrations that still have to be applied, ordered from the oldest to the newest.
	/// </summary>
	Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Applies every pending migration. It is a no-op when the database is already up to date.
	/// </summary>
	Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IDatabaseSchemaMigrator" />
public sealed class DatabaseSchemaMigrator : IDatabaseSchemaMigrator
{
	private readonly DatabaseContext _databaseContext;
	private readonly ILogger<DatabaseSchemaMigrator> _logger;

	public DatabaseSchemaMigrator
		(DatabaseContext databaseContext,
		ILogger<DatabaseSchemaMigrator> logger)
	{
		_databaseContext = databaseContext;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync
		(CancellationToken cancellationToken = default)
	{
		var pendingMigrations =
			await _databaseContext.Database
				.GetPendingMigrationsAsync(cancellationToken: cancellationToken)
				.ToListAsync(cancellationToken: cancellationToken);

		return pendingMigrations;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> MigrateAsync
		(CancellationToken cancellationToken = default)
	{
		var pendingMigrations =
			await GetPendingMigrationsAsync(cancellationToken: cancellationToken);

		if (pendingMigrations.Count == 0)
		{
			_logger.LogInformation
				(message: "The database schema is already up to date; no migration was applied.");

			return pendingMigrations;
		}

		_logger.LogInformation
			(message: "Applying {PendingMigrationCount} pending database migration(s): {PendingMigrations}.",
			PendingMigrationCount: pendingMigrations.Count,
			PendingMigrations: string.Join(", ", pendingMigrations));

		await _databaseContext.Database
			.MigrateAsync(cancellationToken: cancellationToken);

		_logger.LogInformation
			(message: "The database schema was migrated successfully.");

		return pendingMigrations;
	}
}
