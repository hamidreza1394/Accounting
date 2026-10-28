using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

public sealed class RevocationPayloadTests
{
    [Fact]
    public void CanonicalRevocationPayloadUsesStablePropertyOrderAndUtcTimestamp()
    {
        var payload = new LicenseRevocationPayload(
            LicenseAlgorithms.CurrentFormatVersion,
            "license-00000001",
            "install-00000001",
            "FARANOU-BASE",
            3,
            new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.FromHours(2)));

        var json = CanonicalJson.SerializeRevocationPayload(payload);

        Assert.Equal(
            "{\"formatVersion\":1,\"licenseId\":\"license-00000001\",\"installationId\":\"install-00000001\",\"productCode\":\"FARANOU-BASE\",\"sequence\":3,\"revokedAtUtc\":\"2026-10-28T10:00:00.0000000+00:00\"}",
            json);
        Assert.Empty(StructuralValidation.ValidateRevocationPayload(payload));
    }

    [Fact]
    public void RevocationPayloadRejectsUnsupportedVersionAndMissingSequence()
    {
        var payload = new LicenseRevocationPayload(
            LicenseAlgorithms.CurrentFormatVersion + 1,
            "license-00000001",
            "install-00000001",
            "FARANOU-BASE",
            0,
            DateTimeOffset.UtcNow);

        var errors = StructuralValidation.ValidateRevocationPayload(payload);

        Assert.Contains(errors, error => error.Contains("unsupported formatVersion", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("sequence must be >= 1", StringComparison.Ordinal));
    }
}
