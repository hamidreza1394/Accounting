using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorLicenseIssuanceServiceTests
{
    [Fact]
    public async Task RefusesIssuanceOutsideCallerOwnedTransaction()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer("Server=localhost;Database=VendorIssuanceGuardTest;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        await using var db = new VendorDbContext(options);
        var service = new VendorLicenseIssuanceService(
            db,
            new VendorLicenseEnvelopeFactory(new UnavailableLicenseSigningProvider()),
            new VendorAuditWriter(db, TimeProvider.System),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IssueApprovedAsync(
                new VendorApprovalRequest
                {
                    Id = Guid.NewGuid(),
                    ResourceType = "license",
                    ResourceId = Guid.NewGuid(),
                    TargetStatus = "Issued",
                    Status = "Pending",
                    RowVersion = new byte[8],
                    ExpectedResourceRowVersion = new byte[8],
                    RequestedByUserId = Guid.NewGuid()
                },
                Guid.NewGuid(),
                new byte[8],
                null,
                Guid.NewGuid().ToString("N"),
                CancellationToken.None));

        Assert.Contains("approval transaction", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
