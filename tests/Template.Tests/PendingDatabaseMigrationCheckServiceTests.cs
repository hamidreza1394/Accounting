using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Data;
using Infrastructure.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Template.Tests;

public sealed class PendingDatabaseMigrationCheckServiceTests
{
    [Fact]
    public async Task Migration_probe_waits_until_application_started()
    {
        using var lifetime = new TestApplicationLifetime();
        var migrator = new RecordingDatabaseSchemaMigrator();
        var services = new ServiceCollection();
        services.AddScoped<IDatabaseSchemaMigrator>(_ => migrator);
        await using var provider = services.BuildServiceProvider();

        using var service = new PendingDatabaseMigrationCheckService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            lifetime,
            NullLogger<PendingDatabaseMigrationCheckService>.Instance);

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(0, migrator.CallCount);

        lifetime.SignalApplicationStarted();
        await migrator.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, migrator.CallCount);
        await service.StopAsync(CancellationToken.None);
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => _stopped.Token;

        public void SignalApplicationStarted() => _started.Cancel();

        public void StopApplication() => _stopping.Cancel();

        public void Dispose()
        {
            _started.Dispose();
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }

    private sealed class RecordingDatabaseSchemaMigrator : IDatabaseSchemaMigrator
    {
        private int _callCount;

        public TaskCompletionSource<bool> Called { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            Called.TrySetResult(true);
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This regression test only exercises the read-only migration probe.");
    }
}
