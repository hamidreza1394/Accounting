using Microsoft.Extensions.Configuration;
using Xunit;

namespace Faranou.Vendor.Host.Tests.Architecture;

/// <summary>
/// Narrow, purpose-documented enforcement guard (Vendor Entitlement Stage 1): the pure
/// <see cref="Faranou.Licensing.Contracts.EntitlementPolicyEngine"/> must stay connected only at
/// the approved Vendor operational enforcement points. A new operational consumer outside the
/// allow-list fails this test, so entitlement decisions cannot silently bypass the approved
/// enforcement paths. The scan intentionally covers only Vendor production sources — never tests
/// and never other modules — and asserts file names, not implementation details.
/// </summary>
public sealed class VendorEntitlementEnforcementGuardTests
{
    /// <summary>The only production sources allowed to reference the entitlement engine.</summary>
    private static readonly string[] ApprovedEngineConsumers =
    [
        // Provisioning: v1 installation-wide subsystem entitlement check (live operational path).
        "VendorProvisioningLicenseResolver.cs",
        // Provisioning: company-bound (v2) selection resolves the company and its module grants.
        "VendorProvisioningCompanyReferenceRules.cs",
        // Amendments: final-payload policy gate evaluated before any signature is produced.
        "VendorAmendmentEntitlementPolicy.cs"
    ];

    [Fact]
    public void EntitlementPolicyEngineIsConsumedOnlyAtApprovedVendorEnforcementPoints()
    {
        var repoRoot = FindRepositoryRoot();
        var offenders = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "Vendor"), "*.cs", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Where(path => !ApprovedEngineConsumers.Contains(Path.GetFileName(path)))
            .Where(path => File.ReadAllText(path).Contains("EntitlementPolicyEngine"))
            .Select(path => Path.GetRelativePath(repoRoot, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "EntitlementPolicyEngine must only be consumed at the approved Vendor enforcement points " +
            "(provisioning resolver, company-reference rules, amendment policy gate). " +
            $"Offenders: {(offenders.Length == 0 ? "none" : string.Join(", ", offenders))}");
    }

    [Fact]
    public void ApprovedEnforcementPointsStillConnectTheEntitlementEngine()
    {
        var repoRoot = FindRepositoryRoot();
        foreach (var fileName in ApprovedEngineConsumers)
        {
            var path = Directory
                .EnumerateFiles(Path.Combine(repoRoot, "Vendor"), fileName, SearchOption.AllDirectories)
                .FirstOrDefault(IsSourceFile);
            Assert.True(path is not null, $"Approved enforcement point is missing: {fileName}");
            Assert.True(File.ReadAllText(path!).Contains("EntitlementPolicyEngine"),
                $"{fileName} no longer connects the EntitlementPolicyEngine; " +
                "the approved enforcement path must keep using the pure engine.");
        }
    }

    /// <summary>
    /// The company-scoped schema v5 issuance interlock must stay closed: the Customer consumer does
    /// not yet verify v5 or map CompanyReference (#392), so configuration alone must never enable it.
    /// </summary>
    [Fact]
    public void CompanyScopedV5IssuanceInterlockStaysClosed()
    {
        Assert.False(Faranou.Vendor.Host.Authorization.VendorProvisioningCompanyReferenceGate.CustomerConsumerReady);
        Assert.False(Faranou.Vendor.Host.Authorization.VendorProvisioningCompanyReferenceGate.Disabled.IsEnabled);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [Faranou.Vendor.Host.Authorization.VendorProvisioningCompanyReferenceGate.ConfigurationKey] = "true"
            })
            .Build();
        Assert.False(Faranou.Vendor.Host.Authorization.VendorProvisioningCompanyReferenceGate
            .FromConfiguration(configuration).IsEnabled);
    }

    private static bool IsSourceFile(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vendor.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null,
            "Could not locate the repository root (a directory containing Vendor.sln) from the test output path.");
        return directory.FullName;
    }
}
