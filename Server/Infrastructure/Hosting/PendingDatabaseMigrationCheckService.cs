using System;
using System.Threading;
using System.Threading.Tasks;
using Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Hosting;

/// <summary>
/// Checks for pending customer database migrations after the host has fully started.
/// The read-only SQL probe must not delay Windows Service Control Manager startup.
/// </summary>
internal sealed class PendingDatabaseMigrationCheckService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<PendingDatabaseMigrationCheckService> _logger;

    public PendingDatabaseMigrationCheckService(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime applicationLifetime,
        ILogger<PendingDatabaseMigrationCheckService> logger)
    {
        _scopeFactory = scopeFactory;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = _applicationLifetime.ApplicationStarted.Register(
            () => started.TrySetResult(true));

        try
        {
            await started.Task.WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>();
            var pendingMigrations = await migrator.GetPendingMigrationsAsync(stoppingToken);

            if (pendingMigrations.Count > 0)
            {
                _logger.LogWarning(
                    "The database schema is out of date. {PendingMigrationCount} migration(s) are pending: {PendingMigrations}. Apply them with 'dotnet run --project Server -- --migrate'.",
                    pendingMigrations.Count,
                    string.Join(", ", pendingMigrations));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown; do not report an expected cancellation as a database warning.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The pending database migrations could not be checked after host start.");
        }
    }
}
