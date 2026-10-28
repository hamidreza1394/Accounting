using Faranou.Vendor.Persistence.Auditing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorAuditCheckpointTests
{
    [Fact]
    public void CreateNormalizesTimestampToUtcAndProducesStableCanonicalHash()
    {
        var localOffset = VendorAuditCheckpoint.Create(
            eventCount: 3,
            headHash: new string('a', 64),
            createdAtUtc: new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.FromHours(2)));
        var utc = VendorAuditCheckpoint.Create(
            eventCount: 3,
            headHash: new string('a', 64),
            createdAtUtc: new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(TimeSpan.Zero, localOffset.CreatedAtUtc.Offset);
        Assert.Equal(utc.CreatedAtUtc, localOffset.CreatedAtUtc);
        Assert.Equal(utc.CheckpointHash, localOffset.CheckpointHash);
        Assert.True(localOffset.IsValid());
    }

    [Fact]
    public void CreateUsesGenesisHashOnlyForAnEmptyChain()
    {
        var empty = VendorAuditCheckpoint.Create(
            eventCount: 0,
            headHash: VendorAuditChainVerifier.GenesisHash,
            createdAtUtc: DateTimeOffset.UtcNow);
        var populated = VendorAuditCheckpoint.Create(
            eventCount: 1,
            headHash: new string('b', 64),
            createdAtUtc: DateTimeOffset.UtcNow);

        Assert.True(empty.IsValid());
        Assert.True(populated.IsValid());
        Assert.Throws<ArgumentException>(() => VendorAuditCheckpoint.Create(
            eventCount: 0,
            headHash: new string('b', 64),
            createdAtUtc: DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => VendorAuditCheckpoint.Create(
            eventCount: 1,
            headHash: VendorAuditChainVerifier.GenesisHash,
            createdAtUtc: DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsValidRejectsTamperingAndUnknownVersions()
    {
        var checkpoint = VendorAuditCheckpoint.Create(
            eventCount: 2,
            headHash: new string('c', 64),
            createdAtUtc: DateTimeOffset.UtcNow);

        Assert.False((checkpoint with { EventCount = 3 }).IsValid());
        Assert.False((checkpoint with { HeadHash = new string('d', 64) }).IsValid());
        Assert.False((checkpoint with { CreatedAtUtc = checkpoint.CreatedAtUtc.AddSeconds(1) }).IsValid());
        Assert.False((checkpoint with { SchemaVersion = 99 }).IsValid());
        Assert.False((checkpoint with { HashVersion = 99 }).IsValid());
        Assert.False((checkpoint with { CheckpointHash = new string('0', 64) }).IsValid());
    }

    [Fact]
    public void CreateRejectsMalformedHashAndNegativeEventCount()
    {
        Assert.Throws<ArgumentException>(() => VendorAuditCheckpoint.Create(
            eventCount: 1,
            headHash: "not-a-hash",
            createdAtUtc: DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => VendorAuditCheckpoint.Create(
            eventCount: -1,
            headHash: new string('e', 64),
            createdAtUtc: DateTimeOffset.UtcNow));
    }

}
