extern alias CustomerServer;

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Host.Signing;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Identity;
using CustomerProvisioning = CustomerServer::Infrastructure.Provisioning;
using CustomerSecurity = CustomerServer::Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

using CustomerProgram = CustomerServer::Program;

namespace Faranou.Vendor.Host.Tests;

public sealed partial class VendorCustomerMutationHttpIntegrationTests
{
    private const string DualHostVendorKeyId = "dual-host-vendor-key";
    private const string DualHostLicenseKeyId = "dual-host-license-key";
    private const string DualHostAcknowledgementKeyId = "dual-host-customer-ack-key";

    [Fact]
    public async Task Provisioning_TransfersSignedPackageAndAcknowledgementBetweenIndependentHosts_AndConfirmsOnlyAfterEnrollment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var vendorBaseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        var customerBaseConnection = Environment.GetEnvironmentVariable("FARANOU_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(vendorBaseConnection) || string.IsNullOrWhiteSpace(customerBaseConnection))
        {
            const string message = "Dual-host provisioning E2E requires separate disposable Vendor and Customer SQL Server connections.";
            if (Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER") == "1" ||
                Environment.GetEnvironmentVariable("FARANOU_REQUIRE_SQLSERVER") == "1")
                Assert.Fail(message);
            Assert.Skip(message);
            return;
        }

        var vendorDatabaseName = "FaranouVendorProvisioningE2E_" + Guid.NewGuid().ToString("N");
        var customerDatabaseName = "FaranouTests_ProvisioningDualHost_" + Guid.NewGuid().ToString("N");
        var vendorConnection = new SqlConnectionStringBuilder(vendorBaseConnection)
        {
            InitialCatalog = vendorDatabaseName
        }.ConnectionString;
        var customerConnection = new SqlConnectionStringBuilder(customerBaseConnection)
        {
            InitialCatalog = customerDatabaseName
        }.ConnectionString;

        using var vendorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var licenseKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var acknowledgementKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var vendorPublicKey = Convert.ToBase64String(vendorKey.ExportSubjectPublicKeyInfo());
        var licensePublicKey = Convert.ToBase64String(licenseKey.ExportSubjectPublicKeyInfo());
        var acknowledgementPublicKey = Convert.ToBase64String(acknowledgementKey.ExportSubjectPublicKeyInfo());
        var customerReference = "E2E-" + Guid.NewGuid().ToString("N")[..16];
        var installationCode = "E2E-INST-" + Guid.NewGuid().ToString("N")[..12];
        var customerBoundaryId = Guid.NewGuid();
        var companyName = "شرکت آزمون دو میزبان " + Guid.NewGuid().ToString("N")[..8];
        var adminUserName = "admin-" + Guid.NewGuid().ToString("N")[..14];
        var adminPassword = CreateE2ETestPassword();
        var customerInfrastructureUsername = "infra-" + Guid.NewGuid().ToString("N")[..12];
        var customerInfrastructurePassword = CreateE2ETestPassword();
        var customerInfrastructurePasswordHash = new CustomerSecurity.PasswordHashService().Hash(customerInfrastructurePassword);
        var requesterPassword = CreateE2ETestPassword();
        var reviewerPassword = CreateE2ETestPassword();
        var requesterName = "mutation-" + Guid.NewGuid().ToString("N");
        var reviewerName = "reviewer-" + Guid.NewGuid().ToString("N");
        var exchangeDirectory = Path.Combine(Path.GetTempPath(), "FaranouProvisioningE2E-" + Guid.NewGuid().ToString("N"));

        await CreateDatabaseAsync(vendorBaseConnection, vendorDatabaseName, cancellationToken);
        try
        {
            await CreateDatabaseAsync(customerBaseConnection, customerDatabaseName, cancellationToken);
            try
            {
                await using var vendorFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
                {
                    webHost.ConfigureAppConfiguration((_, configuration) =>
                        configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:VendorDb"] = vendorConnection,
                            ["Licensing:TrustedSigningKeys:" + DualHostLicenseKeyId + ":PublicKeySubjectPublicKeyInfoBase64"] = licensePublicKey
                        }));
                    webHost.ConfigureTestServices(services =>
                    {
                        services.RemoveAll<IProvisioningSigningProvider>();
                        services.AddSingleton<IProvisioningSigningProvider>(new DualHostProvisioningSigner(vendorKey));
                        services.RemoveAll<ProvisioningPackageSigningTrustStore>();
                        services.AddSingleton(new ProvisioningPackageSigningTrustStore(
                        [
                            new ProvisioningPackageSigningTrustedKeyOptions
                            {
                                KeyId = DualHostVendorKeyId,
                                PublicKeySubjectPublicKeyInfoBase64 = vendorPublicKey
                            }
                        ]));
                        services.RemoveAll<ProvisioningAcknowledgementTrustStore>();
                        services.AddSingleton(new ProvisioningAcknowledgementTrustStore(
                        [
                            new ProvisioningAcknowledgementTrustedKeyOptions
                            {
                                KeyId = DualHostAcknowledgementKeyId,
                                PublicKeySubjectPublicKeyInfoBase64 = acknowledgementPublicKey
                            }
                        ]));
                    });
                });

                await using var customerFactory = new WebApplicationFactory<CustomerProgram>().WithWebHostBuilder(webHost =>
                {
                    webHost.UseSetting("ConnectionStrings:ConnectionString", customerConnection);
                    webHost.UseSetting("ApplicationSettings:MasterUsername", customerInfrastructureUsername);
                    webHost.UseSetting("ApplicationSettings:MasterPassword", customerInfrastructurePasswordHash);
                    webHost.UseSetting("ApplicationSettings:AllowLegacyMasterPasswordHash", "false");
                    webHost.ConfigureAppConfiguration((_, configuration) =>
                        configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:ConnectionString"] = customerConnection,
                            ["ApplicationSettings:MasterUsername"] = customerInfrastructureUsername,
                            ["ApplicationSettings:MasterPassword"] = customerInfrastructurePasswordHash,
                            ["ApplicationSettings:AllowLegacyMasterPasswordHash"] = "false",
                            ["Provisioning:CustomerReference"] = customerReference,
                            ["Provisioning:InstallationId"] = installationCode,
                            ["Provisioning:CustomerBoundaryId"] = customerBoundaryId.ToString("D"),
                            ["Provisioning:TrustedVendorKeys:" + DualHostVendorKeyId] = vendorPublicKey,
                            ["Provisioning:TrustedAcknowledgementKeys:" + DualHostAcknowledgementKeyId] = acknowledgementPublicKey
                        }));
                    webHost.ConfigureTestServices(services =>
                    {
                        services.RemoveAll<CustomerProvisioning.ICustomerProvisioningAcknowledgementSigningProvider>();
                        services.AddSingleton<CustomerProvisioning.ICustomerProvisioningAcknowledgementSigningProvider>(
                            new DualHostAcknowledgementSigner(acknowledgementKey));
                    });
                });

                await using (var vendorScope = vendorFactory.Services.CreateAsyncScope())
                {
                    var db = vendorScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                    await db.Database.MigrateAsync(cancellationToken);

                    var roles = vendorScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                    var role = new IdentityRole<Guid>("dual-host-provisioning-" + Guid.NewGuid().ToString("N")[..8])
                    {
                        Id = Guid.NewGuid()
                    };
                    var roleResult = await roles.CreateAsync(role);
                    Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Code)));

                    foreach (var permission in new[]
                    {
                        VendorPermissionCatalog.CustomersRead,
                        VendorPermissionCatalog.CustomersManage,
                        VendorPermissionCatalog.InstallationsRead,
                        VendorPermissionCatalog.InstallationsManage,
                        VendorPermissionCatalog.ApprovalsRead,
                        VendorPermissionCatalog.ApprovalsRequest,
                        VendorPermissionCatalog.ApprovalsReview
                    })
                    {
                        var result = await roles.AddClaimAsync(
                            role, new Claim(VendorPermissionCatalog.ClaimType, permission));
                        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Code)));
                    }

                    var users = vendorScope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
                    foreach (var (userName, password) in new[]
                    {
                        (requesterName, requesterPassword),
                        (reviewerName, reviewerPassword)
                    })
                    {
                        var user = new VendorAdminUser
                        {
                            UserName = userName,
                            Email = userName + "@example.invalid",
                            EmailConfirmed = true,
                            IsEnabled = true,
                            MustChangePassword = false,
                            CreatedAtUtc = DateTimeOffset.UtcNow
                        };
                        var createResult = await users.CreateAsync(user, password);
                        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Code)));
                        var addRoleResult = await users.AddToRoleAsync(user, role.Name!);
                        Assert.True(addRoleResult.Succeeded, string.Join("; ", addRoleResult.Errors.Select(error => error.Code)));
                        var resetResult = await users.ResetAuthenticatorKeyAsync(user);
                        Assert.True(resetResult.Succeeded);
                        var mfaResult = await users.SetTwoFactorEnabledAsync(user, true);
                        Assert.True(mfaResult.Succeeded);
                    }

                    var customer = new VendorCustomer
                    {
                        Id = Guid.NewGuid(),
                        CustomerNumber = customerReference,
                        LegalName = "Dual Host E2E Customer",
                        DisplayName = "Dual Host E2E Customer",
                        Status = "Active",
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        UpdatedAtUtc = DateTimeOffset.UtcNow
                    };
                    var installation = new VendorInstallation
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = customer.Id,
                        InstallationCode = installationCode,
                        Name = "Independent Customer Host",
                        Environment = "Testing",
                        Status = "Active",
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    };
                    db.Customers.Add(customer);
                    db.Installations.Add(installation);
                    await db.SaveChangesAsync(cancellationToken);

                    var licenseNow = DateTimeOffset.UtcNow;
                    var license = new VendorLicense
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = customer.Id,
                        InstallationId = installation.Id,
                        LicenseNumber = "E2E-LIC-" + Guid.NewGuid().ToString("N")[..12],
                        ProductCode = "FARANOU-BASE",
                        Status = "Issued",
                        LicenseKind = LicensePayload.KindPerpetual,
                        MaxCompanies = 10,
                        SubsystemsJson = "[\"finance\",\"sales\"]",
                        GraceDays = 30,
                        LastIssuedSequence = 1,
                        ValidFromUtc = licenseNow.AddMinutes(-5),
                        CreatedAtUtc = licenseNow
                    };
                    // ... rest of the file would be here, but truncated for this example; in real it would be full
                }
            }
        }
    }
}
