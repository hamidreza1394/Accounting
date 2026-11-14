using System.Text;
using Faranou.Modules.Trade.Infrastructure.Persistence;
using Infrastructure.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// PR-1: the Migration Host applies Core first, Trade only on explicit request, keeps one run at a time with a
/// SQL Server application lock, resumes after a failure, never baselines a legacy Trade schema and never prints
/// connection settings. Behaviour that depends on SQL Server (history tables, the lock, legacy tables) runs on a
/// real throw-away SQL Server database; failures that cannot be produced from outside use a failing runner.
/// </summary>
public sealed class DatabaseMigrationHostSqlServerTests
{
	private static bool SqlServerAvailable()
	{
		if (IntegrationDatabase.UsesSqlServer)
			return true;
		if (Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
			throw new InvalidOperationException("FARANOU_REQUIRE_SQLSERVER=1 but FARANOU_TEST_SQLSERVER is not set.");
		return false;
	}

	private static string ConnectionFor(string databaseName) =>
		new SqlConnectionStringBuilder(IntegrationDatabase.SqlServerConnectionString!) { InitialCatalog = databaseName }.ConnectionString;

	private static Data.DatabaseContext Core(string connection) =>
		new(new DbContextOptionsBuilder<Data.DatabaseContext>()
			.UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(Data.DatabaseContext).Assembly.FullName))
			.Options);

	private static TradeDbContext TradeContext(string connection)
	{
		var options = new DbContextOptionsBuilder<TradeDbContext>();
		TradeDbContextOptionsConfiguration.ConfigureSqlServer(options, connection);
		return new TradeDbContext(options.Options);
	}

	private sealed class Console2 : IDisposable
	{
		public StringWriter Writer { get; } = new();
		public string Text => Writer.ToString();
		public void Dispose() => Writer.Dispose();
	}

	private static DatabaseMigrationHost RealHost(string connection, Data.DatabaseContext core, TradeDbContext trade, TextWriter output) =>
		new(connection, new EfContextMigrationRunner("Core", core), new TradeMigrationRunner(trade), output);

	private static async Task<int> CountAsync(string connection, string sql)
	{
		await using var c = new SqlConnection(connection);
		await c.OpenAsync(TestContext.Current.CancellationToken);
		await using var command = new SqlCommand(sql, c);
		return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
	}

	private static Task<int> HistoryRows(string connection, string table) =>
		CountAsync(connection, $"IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NULL SELECT -1 ELSE EXEC(N'SELECT COUNT(*) FROM [dbo].[{table}]')");

	private static Task<int> TradeTables(string connection) =>
		CountAsync(connection, "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = N'trade'");

	private static async Task WithDatabaseAsync(Func<string, Task> body)
	{
		var name = "FaranouTests_MIG_" + Guid.NewGuid().ToString("N")[..12];
		var connection = ConnectionFor(name);
		try
		{
			await body(connection);
		}
		finally
		{
			await using var cleanup = Core(connection);
			SqlConnection.ClearAllPools();
			await cleanup.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
		}
	}

	[Fact]
	public async Task EmptyDatabase_CoreOnly_WithoutExplicitTradeRequest_TradeIsNotApplied()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		await WithDatabaseAsync(async connection =>
		{
			using var output = new Console2();
			await using var core = Core(connection);
			await using var trade = TradeContext(connection);

			var outcome = await RealHost(connection, core, trade, output.Writer).RunAsync(includeTrade: false, TestContext.Current.CancellationToken);

			Assert.True(outcome == DatabaseMigrationOutcome.Succeeded, output.Text);
			Assert.True(await HistoryRows(connection, "__EFMigrationsHistory") > 0);
			Assert.Equal(-1, await HistoryRows(connection, "__TradeMigrationsHistory"));
			Assert.Equal(0, await TradeTables(connection));
			Assert.Contains("Trade: skipped", output.Text);
		});
	}

	[Fact]
	public async Task CoreThenTrade_WithExplicitRequest_UsesSeparateHistories_AndRerunRepeatsNothing()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		await WithDatabaseAsync(async connection =>
		{
			using var output = new Console2();
			await using var core = Core(connection);
			await using var trade = TradeContext(connection);
			var host = RealHost(connection, core, trade, output.Writer);

			Assert.Equal(DatabaseMigrationOutcome.Succeeded, await host.RunAsync(true, TestContext.Current.CancellationToken));
			var coreRows = await HistoryRows(connection, "__EFMigrationsHistory");
			var tradeRows = await HistoryRows(connection, "__TradeMigrationsHistory");
			Assert.True(coreRows > 0);
			Assert.True(tradeRows > 0);
			Assert.Equal(3, await TradeTables(connection));
			Assert.True(output.Text.IndexOf("Core: applying", StringComparison.Ordinal) < output.Text.IndexOf("Trade: applying", StringComparison.Ordinal));

			var second = new StringWriter();
			Assert.Equal(DatabaseMigrationOutcome.Succeeded,
				await RealHost(connection, core, trade, second).RunAsync(true, TestContext.Current.CancellationToken));
			Assert.Contains("Core: up to date", second.ToString());
			Assert.Contains("Trade: up to date", second.ToString());
			Assert.Equal(coreRows, await HistoryRows(connection, "__EFMigrationsHistory"));
			Assert.Equal(tradeRows, await HistoryRows(connection, "__TradeMigrationsHistory"));
		});
	}

	[Fact]
	public async Task LegacyTradeTablesWithoutHistory_StopWithoutBaselineOrChange()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		await WithDatabaseAsync(async connection =>
		{
			using var output = new Console2();
			await using var core = Core(connection);
			await using var trade = TradeContext(connection);
			Assert.Equal(DatabaseMigrationOutcome.Succeeded,
				await RealHost(connection, core, trade, TextWriter.Null).RunAsync(false, TestContext.Current.CancellationToken));

			// A legacy shape: the trade schema and a table exist, but no __TradeMigrationsHistory.
			await using (var c = new SqlConnection(connection))
			{
				await c.OpenAsync(TestContext.Current.CancellationToken);
				await using var command = new SqlCommand(
					"EXEC(N'CREATE SCHEMA [trade]'); EXEC(N'CREATE TABLE [trade].[Parties] ([Id] uniqueidentifier NOT NULL PRIMARY KEY, [Marker] nvarchar(10) NULL)'); " +
					"INSERT INTO [trade].[Parties] ([Id],[Marker]) VALUES (NEWID(), N'legacy');", c);
				await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
			}

			var outcome = await RealHost(connection, core, trade, output.Writer).RunAsync(true, TestContext.Current.CancellationToken);

			Assert.Equal(DatabaseMigrationOutcome.TradeStateInconsistent, outcome);
			Assert.Equal(-1, await HistoryRows(connection, "__TradeMigrationsHistory"));
			Assert.Equal(1, await TradeTables(connection));
			Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [trade].[Parties] WHERE [Marker] = N'legacy'"));
			Assert.Contains("not baselined automatically", output.Text);
		});
	}

	[Fact]
	public async Task SecondConcurrentRun_IsRefused_AndTheLockIsFreeAfterTheFirstRunEnds()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		await WithDatabaseAsync(async connection =>
		{
			using var output = new Console2();
			var gate = new TaskCompletionSource();
			var entered = new TaskCompletionSource();
			var blocking = new BlockingRunner(entered, gate.Task);
			var first = new DatabaseMigrationHost(connection, blocking, new NoopRunner("Trade"), TextWriter.Null);
			// The lock is database-scoped, so the database must exist before a lock connection can open.
			await using (var create = Core(connection))
				await create.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

			var firstRun = first.RunAsync(false, TestContext.Current.CancellationToken);
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

			var second = new DatabaseMigrationHost(connection, new NoopRunner("Core"), new NoopRunner("Trade"), output.Writer);
			Assert.Equal(DatabaseMigrationOutcome.LockNotAcquired, await second.RunAsync(false, TestContext.Current.CancellationToken));
			Assert.Contains("another migration run holds the database lock", output.Text);

			gate.SetResult();
			Assert.Equal(DatabaseMigrationOutcome.Succeeded, await firstRun);
			Assert.Equal(DatabaseMigrationOutcome.Succeeded,
				await second.RunAsync(false, TestContext.Current.CancellationToken));
		});
	}

	[Fact]
	public async Task LockIsReleased_WhenAStepFails()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		await WithDatabaseAsync(async connection =>
		{
			await using (var create = Core(connection))
				await create.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

			var failing = new DatabaseMigrationHost(connection, new FailingRunner("Core"), new NoopRunner("Trade"), TextWriter.Null);
			Assert.Equal(DatabaseMigrationOutcome.CoreFailed, await failing.RunAsync(true, TestContext.Current.CancellationToken));

			var next = new DatabaseMigrationHost(connection, new NoopRunner("Core"), new NoopRunner("Trade"), TextWriter.Null);
			Assert.Equal(DatabaseMigrationOutcome.Succeeded, await next.RunAsync(false, TestContext.Current.CancellationToken));
		});
	}

	[Fact]
	public async Task CoreFailure_TradeIsNeverStarted_AndTheErrorHidesConnectionSettings()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		await WithDatabaseAsync(async connection =>
		{
			await using (var create = Core(connection))
				await create.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
			using var output = new Console2();
			var trade = new NoopRunner("Trade");

			var outcome = await new DatabaseMigrationHost(connection, new FailingRunner("Core"), trade, output.Writer)
				.RunAsync(true, TestContext.Current.CancellationToken);

			Assert.Equal(DatabaseMigrationOutcome.CoreFailed, outcome);
			Assert.Equal(0, trade.Calls);
			Assert.Contains("Core: FAILED", output.Text);
			Assert.DoesNotContain("S3cret!Pw", output.Text);
			Assert.DoesNotContain("tcp:secret-host", output.Text);
			Assert.DoesNotContain("secret-user", output.Text);
		});
	}

	[Fact]
	public async Task TradeFailure_IsNotSuccess_AndARerunContinuesFromTheFailedStep()
	{
		Assert.SkipUnless(SqlServerAvailable(), "SQL Server is not configured (FARANOU_TEST_SQLSERVER).");
		await WithDatabaseAsync(async connection =>
		{
			await using var core = Core(connection);
			var coreRunner = new CountingRunner(new EfContextMigrationRunner("Core", core));
			await using var tradeContext = TradeContext(connection);
			var flaky = new FlakyTradeRunner(new TradeMigrationRunner(tradeContext));

			using var first = new Console2();
			var outcome = await new DatabaseMigrationHost(connection, coreRunner, flaky, first.Writer)
				.RunAsync(true, TestContext.Current.CancellationToken);
			Assert.Equal(DatabaseMigrationOutcome.TradeFailed, outcome);
			Assert.True(await HistoryRows(connection, "__EFMigrationsHistory") > 0);
			Assert.Equal(-1, await HistoryRows(connection, "__TradeMigrationsHistory"));

			using var second = new Console2();
			var coreRowsBefore = await HistoryRows(connection, "__EFMigrationsHistory");
			outcome = await new DatabaseMigrationHost(connection, coreRunner, flaky, second.Writer)
				.RunAsync(true, TestContext.Current.CancellationToken);
			Assert.Equal(DatabaseMigrationOutcome.Succeeded, outcome);
			Assert.Contains("Core: up to date", second.Text);
			Assert.Equal(coreRowsBefore, await HistoryRows(connection, "__EFMigrationsHistory"));
			Assert.Equal(3, await TradeTables(connection));
		});
	}

	[Fact]
	public void Redaction_RemovesConnectionSettingsFromMessages()
	{
		var text = DatabaseMigrationHost.Redact(
			"Cannot open. Data Source=tcp:secret-host,1433;Initial Catalog=Prod;User ID=secret-user;Password=S3cret!Pw;Encrypt=True");

		Assert.DoesNotContain("secret-host", text);
		Assert.DoesNotContain("secret-user", text);
		Assert.DoesNotContain("S3cret!Pw", text);
		Assert.DoesNotContain("Prod", text);
	}

	private class NoopRunner(string name) : IContextMigrationRunner
	{
		public int Calls;
		public string ContextName { get; } = name;
		public virtual Task<string?> CheckPreconditionAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
		public virtual Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken)
		{
			Calls++;
			return Task.FromResult<IReadOnlyList<string>>([]);
		}
		public virtual Task MigrateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class FailingRunner(string name) : NoopRunner(name)
	{
		public override Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken) =>
			throw new InvalidOperationException(
				"Login failed. Data Source=tcp:secret-host,1433;User ID=secret-user;Password=S3cret!Pw;");
	}

	private sealed class BlockingRunner(TaskCompletionSource entered, Task gate) : NoopRunner("Core")
	{
		public override async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken)
		{
			entered.TrySetResult();
			await gate;
			return [];
		}
	}

	private sealed class CountingRunner(IContextMigrationRunner inner) : IContextMigrationRunner
	{
		public string ContextName => inner.ContextName;
		public Task<string?> CheckPreconditionAsync(CancellationToken cancellationToken) => inner.CheckPreconditionAsync(cancellationToken);
		public Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken) => inner.GetPendingMigrationsAsync(cancellationToken);
		public Task MigrateAsync(CancellationToken cancellationToken) => inner.MigrateAsync(cancellationToken);
	}

	/// <summary>Fails the first <c>MigrateAsync</c> before anything is applied; later calls delegate to the real runner.</summary>
	private sealed class FlakyTradeRunner(IContextMigrationRunner inner) : IContextMigrationRunner
	{
		private bool failed;
		public string ContextName => inner.ContextName;
		public Task<string?> CheckPreconditionAsync(CancellationToken cancellationToken) => inner.CheckPreconditionAsync(cancellationToken);
		public Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken) => inner.GetPendingMigrationsAsync(cancellationToken);
		public Task MigrateAsync(CancellationToken cancellationToken)
		{
			if (!failed)
			{
				failed = true;
				throw new InvalidOperationException("simulated Trade failure");
			}
			return inner.MigrateAsync(cancellationToken);
		}
	}
}
