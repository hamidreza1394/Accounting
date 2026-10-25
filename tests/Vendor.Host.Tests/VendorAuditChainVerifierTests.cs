using Faranou.Vendor.Persistence.Auditing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorAuditChainVerifierTests
{
    [Fact]
    public void VerifyRejectsFreeFormMetadataEvenBeforeHashValidation()
    {
        var auditEvent = new VendorAuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ActionCode = "test.audit-event",
            SubjectType = "VendorAdminUser",
            SubjectId = Guid.NewGuid().ToString("N"),
            PreviousHash = VendorAuditChainVerifier.GenesisHash,
            EventHash = new string('0', 64),
            MetadataJson = "{\"password\":\"must-never-be-accepted\"}"
        };

        var result = VendorAuditChainVerifier.Verify([auditEvent]);

        Assert.False(result.IsValid);
        Assert.Equal("invalid-event-fields-or-unapproved-metadata", result.FailureCode);
        Assert.Null(result.HeadHash);
    }

    [Fact]
    public void VerifyRejectsNonOpaqueSubjectIdentifiers()
    {
        var auditEvent = new VendorAuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ActionCode = "auth.admin-disabled",
            SubjectType = "VendorAdminUser",
            SubjectId = "operator@example.com",
            PreviousHash = VendorAuditChainVerifier.GenesisHash,
            EventHash = new string('0', 64)
        };

        var result = VendorAuditChainVerifier.Verify([auditEvent]);

        Assert.False(result.IsValid);
        Assert.Equal("invalid-event-fields-or-unapproved-metadata", result.FailureCode);
    }
}
