using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Hosting;

/// <summary>Process exit codes of <c>Server.dll --migrate</c>.</summary>
public enum DatabaseMigrationOutcome
{
	Succeeded = 0,
	CoreFailed = 1,
	TradeFailed = 2,
	LockNotAcquired = 3,
	TradeStateInconsistent = 4,
}

/// <summary>One EF Core context whose migrations the host applies (own history table).</summary>
public interface IContextMigrationRunner
{
	string ContextName { get; }

	/// <summary>Returns a message when the database state forbids migrating, otherwise null.</summary>
	Task<string?> CheckPreconditionAsync(CancellationToken cancellationToken);

	Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken);

	Task MigrateAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation: pending list and migrate on the context's own history table.</summary>
public class EfContextMigrationRunner(string contextName, DbContext context) : IContextMigrationRunner
{
	public string ContextName { get; } = contextName;

	public virtual Task<string?> CheckPreconditionAsync(CancellationToken cancellationToken) =>
		Task.FromResult<string?>(null);

	public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken) =>
		(await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

	public Task MigrateAsync(CancellationToken cancellationToken) =>
		context.Database.MigrateAsync(cancellationToken);
}

/// <summary>
/// Trade guard: tables of schema <c>trade</c> without any row in <c>__TradeMigrationsHistory</c> are a legacy
/// state. The history is never filled in by guesswork (no automatic baseline) and nothing is dropped.
/// </summary>
public sealed class TradeMigrationRunner(DbContext context) : EfContextMigrationRunner("Trade", context)
{
	private readonly DbContext _context = context;

	public override async Task<string?> CheckPreconditionAsync(CancellationToken cancellationToken)
	{
		var connection = _context.Database.GetDbConnection();
		var openedHere = connection.State != ConnectionState.Open;
		if (openedHere)
		{
			await connection.OpenAsync(cancellationToken);
		}

		try
		{
			await using var command = connection.CreateCommand();
			command.CommandText =
				"SELECT " +
				"(SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = N'trade'), " +
				"(SELECT COUNT(*) FROM sys.tables WHERE name = N'__TradeMigrationsHistory' AND schema_id = SCHEMA_ID(N'dbo'));";
			int tradeTables;
			bool historyExists;
			await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
			{
				await reader.ReadAsync(cancellationToken);
				tradeTables = reader.GetInt32(0);
				historyExists = reader.GetInt32(1) > 0;
			}

			var historyRows = 0;
			if (historyExists)
			{
				await using var count = connection.CreateCommand();
				count.CommandText = "SELECT COUNT(*) FROM [dbo].[__TradeMigrationsHistory];";
				historyRows = System.Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken));
			}

			return tradeTables > 0 && historyRows == 0
				? $"Trade tables exist ({tradeTables}) but __TradeMigrationsHistory is missing or empty. " +
				  "The history is not baselined automatically and no table is changed. " +
				  "Follow the legacy-database procedure in docs/DATABASE.md, then run the command again."
				: null;
		}
		finally
		{
			if (openedHere)
			{
				await connection.CloseAsync();
			}
		}
	}
}

/// <summary>
/// Applies the Core migrations, then (only on explicit request) the Trade migrations, to one database.
/// A SQL Server application lock held on a dedicated connection keeps two runs from overlapping; it is
/// released on success, failure and cancellation, and by the server when the process dies.
/// </summary>
public sealed partial class DatabaseMigrationHost(
	string connectionString,
	IContextMigrationRunner core,
	IContextMigrationRunner trade,
	TextWriter output)
{
	public const string LockResource = "Farano.DatabaseMigration";

	public async Task<DatabaseMigrationOutcome> RunAsync(bool includeTrade, CancellationToken cancellationToken = default)
	{
		// The lock connection never uses the pool: a failed open against a not-yet-created database must not
		// leave a cached login error in the pool that the migration itself (same connection string) uses.
		var target = new SqlConnectionStringBuilder(connectionString) { Pooling = false };
		var resource = $"{LockResource}:{target.InitialCatalog}";
		var lockConnection = new SqlConnection(target.ConnectionString);
		try
		{
			try
			{
				await lockConnection.OpenAsync(cancellationToken);
			}
			catch (SqlException exception) when (exception.Number == 4060)
			{
				// The target database does not exist yet (EF creates it). Lock through master, keyed by the
				// target database name, so two runs against the same new database still exclude each other.
				await lockConnection.DisposeAsync();
				target.InitialCatalog = "master";
				lockConnection = new SqlConnection(target.ConnectionString);
				await lockConnection.OpenAsync(cancellationToken);
			}

			if (!await TryAcquireLockAsync(lockConnection, resource, cancellationToken))
			{
				await lockConnection.DisposeAsync();
				output.WriteLine("[migrate] FAILED: another migration run holds the database lock. Nothing was applied.");
				return DatabaseMigrationOutcome.LockNotAcquired;
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			await lockConnection.DisposeAsync();
			output.WriteLine($"[migrate] FAILED: cannot take the database lock ({Describe(exception)}).");
			return DatabaseMigrationOutcome.LockNotAcquired;
		}

		try
		{
			if (!await RunStepAsync(core, cancellationToken))
			{
				output.WriteLine("[migrate] Trade was not started because Core failed.");
				return DatabaseMigrationOutcome.CoreFailed;
			}

			if (!includeTrade)
			{
				output.WriteLine("[migrate] Trade: skipped (not requested; use --include-trade).");
				return DatabaseMigrationOutcome.Succeeded;
			}

			var precondition = await SafeAsync(() => trade.CheckPreconditionAsync(cancellationToken));
			if (precondition.Error is not null)
			{
				output.WriteLine($"[migrate] Trade: FAILED while checking the database ({precondition.Error}).");
				return DatabaseMigrationOutcome.TradeFailed;
			}
			if (precondition.Value is not null)
			{
				output.WriteLine($"[migrate] Trade: STOPPED. {precondition.Value}");
				return DatabaseMigrationOutcome.TradeStateInconsistent;
			}

			return await RunStepAsync(trade, cancellationToken)
				? DatabaseMigrationOutcome.Succeeded
				: DatabaseMigrationOutcome.TradeFailed;
		}
		finally
		{
			await ReleaseLockAsync(lockConnection, resource);
			await lockConnection.DisposeAsync();
		}
	}

	private async Task<bool> RunStepAsync(IContextMigrationRunner runner, CancellationToken cancellationToken)
	{
		try
		{
			var pending = await runner.GetPendingMigrationsAsync(cancellationToken);
			if (pending.Count == 0)
			{
				output.WriteLine($"[migrate] {runner.ContextName}: up to date, nothing applied.");
				return true;
			}

			output.WriteLine($"[migrate] {runner.ContextName}: applying {pending.Count} migration(s): {string.Join(", ", pending)}");
			await runner.MigrateAsync(cancellationToken);
			output.WriteLine($"[migrate] {runner.ContextName}: applied successfully.");
			return true;
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			output.WriteLine($"[migrate] {runner.ContextName}: FAILED ({Describe(exception)}). Run the command again to continue from the failed step.");
			return false;
		}
	}

	private static async Task<(string? Value, string? Error)> SafeAsync(Func<Task<string?>> action)
	{
		try
		{
			return (await action(), null);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return (null, Describe(exception));
		}
	}

	private static async Task<bool> TryAcquireLockAsync(SqlConnection connection, string resource, CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText =
			"DECLARE @r int; EXEC @r = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
			"@LockOwner = 'Session', @LockTimeout = 0; SELECT @r;";
		command.Parameters.Add(new SqlParameter("@resource", resource));
		var result = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
		return result >= 0;
	}

	private static async Task ReleaseLockAsync(SqlConnection connection, string resource)
	{
		try
		{
			await using var command = connection.CreateCommand();
			command.CommandText = "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
			command.Parameters.Add(new SqlParameter("@resource", resource));
			await command.ExecuteNonQueryAsync();
		}
		catch (Exception)
		{
			// The lock is bound to this connection; disposing it releases the lock on the server.
		}
	}

	/// <summary>Exception type, SQL error number and a message with connection settings and secrets removed.</summary>
	public static string Describe(Exception exception)
	{
		var number = exception is SqlException sql ? $", SQL error {sql.Number}" : string.Empty;
		return $"{exception.GetType().Name}{number}: {Redact(exception.Message)}";
	}

	public static string Redact(string message) => SecretPattern().Replace(message, "$1=***");

	[GeneratedRegex(@"(?i)\b(password|pwd|user id|uid|data source|server|initial catalog|database|authentication)\s*=\s*[^;'""\r\n]*")]
	private static partial Regex SecretPattern();
}
