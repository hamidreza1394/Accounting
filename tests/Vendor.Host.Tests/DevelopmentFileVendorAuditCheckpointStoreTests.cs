using Faranou.Vendor.Persistence.Auditing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class DevelopmentFileVendorAuditCheckpointStoreTests
{
    [Fact]
    public async Task AppendAndRead_PersistsValidMonotonicCheckpoints()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FaranouAuditCheckpointTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "checkpoints.jsonl");
        try
        {
            var store = new DevelopmentFileVendorAuditCheckpointStore(path);
            var first = VendorAuditCheckpoint.Create(1, new string('a', 64), DateTimeOffset.UtcNow);
            var second = VendorAuditCheckpoint.Create(2, new string('b', 64), DateTimeOffset.UtcNow);

            await store.AppendAsync(first);
            await store.AppendAsync(second);

            var latest = await store.ReadLatestAsync(TestContext.Current.CancellationToken);
            Assert.Equal(second, latest);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Append_RejectsDuplicateOrDecreasingEventCount()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FaranouAuditCheckpointTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "checkpoints.jsonl");
        try
        {
            var store = new DevelopmentFileVendorAuditCheckpointStore(path);
            var first = VendorAuditCheckpoint.Create(2, new string('a', 64), DateTimeOffset.UtcNow);
            await store.AppendAsync(first);

            await Assert.ThrowsAsync<InvalidOperationException>(() => store.AppendAsync(first));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.AppendAsync(VendorAuditCheckpoint.Create(1, new string('b', 64), DateTimeOffset.UtcNow)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReadLatest_FailsClosedWhenFileContainsTamperedCheckpoint()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FaranouAuditCheckpointTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "checkpoints.jsonl");
        try
        {
            var store = new DevelopmentFileVendorAuditCheckpointStore(path);
            var checkpoint = VendorAuditCheckpoint.Create(1, new string('a', 64), DateTimeOffset.UtcNow);
            await store.AppendAsync(checkpoint);
            var contents = await File.ReadAllTextAsync(path);
            await File.WriteAllTextAsync(path, contents.Replace(checkpoint.HeadHash, new string('b', 64), StringComparison.Ordinal));

            await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadLatestAsync());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReadLatest_WhenLockWaitIsCancelled_ReleasesProcessGate()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FaranouAuditCheckpointTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "checkpoints.jsonl");
        Directory.CreateDirectory(directory);

        try
        {
            var store = new DevelopmentFileVendorAuditCheckpointStore(path);
            await using var heldLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadLatestAsync(cancellation.Token));

            await heldLock.DisposeAsync();
            Assert.Null(await store.ReadLatestAsync());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AppendAcrossIndependentProcessesRejectsDuplicateSequenceWithoutCorruptingJournal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FaranouAuditCheckpointProcessTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "checkpoints.jsonl");
        Directory.CreateDirectory(directory);

        try
        {
            var configuration = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.Name
                ?? throw new InvalidOperationException("Test configuration directory could not be resolved.");
            var helperProject = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "../../../../Vendor.CheckpointProcessHelper"));
            var helperDll = Path.Combine(helperProject, "bin", configuration, "net10.0",
                "Vendor.CheckpointProcessHelper.dll");
            Assert.True(File.Exists(helperDll), $"Cross-process helper was not built: {helperDll}");

            var attempts = Enumerable.Range(0, 8)
                .Select(_ => RunAppendProcessAsync(helperDll, path, sequence: 1, new string('a', 64)))
                .ToArray();
            var results = await Task.WhenAll(attempts);

            Assert.Single(results, result => result.ExitCode == 0);
            Assert.Equal(7, results.Count(result => result.ExitCode != 0));
            Assert.All(results.Where(result => result.ExitCode != 0), result =>
                Assert.Contains("InvalidOperationException", result.StandardError, StringComparison.Ordinal));

            var store = new DevelopmentFileVendorAuditCheckpointStore(path);
            var latest = await store.ReadLatestAsync();
            Assert.NotNull(latest);
            Assert.Equal(1, latest.EventCount);
            Assert.Single(await File.ReadAllLinesAsync(path, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static async Task<ProcessResult> RunAppendProcessAsync(
        string helperDll,
        string journalPath,
        int sequence,
        string headHash)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(helperDll);
        startInfo.ArgumentList.Add("append");
        startInfo.ArgumentList.Add(journalPath);
        startInfo.ArgumentList.Add(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(headHash);

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start checkpoint helper process.");
        var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Checkpoint helper process did not exit within 30 seconds.");
        }

        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    [Fact]
    public void Constructor_RequiresFullyQualifiedPath()
    {
        Assert.Throws<ArgumentException>(() => new DevelopmentFileVendorAuditCheckpointStore("relative/checkpoints.jsonl"));
    }
}
