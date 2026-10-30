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

            var latest = await store.ReadLatestAsync();
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
    public void Constructor_RequiresFullyQualifiedPath()
    {
        Assert.Throws<ArgumentException>(() => new DevelopmentFileVendorAuditCheckpointStore("relative/checkpoints.jsonl"));
    }
}
