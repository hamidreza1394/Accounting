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

                await using (var customerValidationScope = customerFactory.Services.CreateAsyncScope())
                {
                    var customerServices = customerValidationScope.ServiceProvider;
                    Assert.NotNull(customerServices.GetRequiredService<ICustomerLicenseEnvelopeValidator>());
                    Assert.Null(customerServices.GetService<LicenseEnvelopeValidator>());
                    Assert.Null(customerServices.GetService<RevocationAwareLicenseEnvelopeValidator>());
                }

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
                    var licensePayload = new LicensePayload(
                        1, license.Id.ToString("D"), installation.Id.ToString("D"),
                        license.ProductCode, license.LicenseKind, licenseNow,
                        license.ValidFromUtc, null, license.GraceDays, 1,
                        new LicenseLimits(license.MaxCompanies, ["finance", "sales"]));
                    var licensePayloadJson = CanonicalJson.SerializePayload(licensePayload);
                    var licenseSignature = licenseKey.SignData(
                        Encoding.UTF8.GetBytes(licensePayloadJson),
                        HashAlgorithmName.SHA256,
                        DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                    var licenseEnvelope = new LicenseEnvelope(
                        1, DualHostLicenseKeyId, LicenseAlgorithms.EcdsaP256Sha256,
                        licensePayloadJson, Convert.ToBase64String(licenseSignature));
                    db.Licenses.Add(license);
                    db.SignedLicenseEnvelopes.Add(new VendorSignedLicenseEnvelope
                    {
                        Id = Guid.NewGuid(),
                        LicenseId = license.Id,
                        Sequence = 1,
                        KeyId = licenseEnvelope.Kid,
                        Algorithm = licenseEnvelope.Algorithm,
                        EnvelopeJson = JsonSerializer.Serialize(licenseEnvelope),
                        PayloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(licensePayloadJson))).ToLowerInvariant(),
                        CreatedAtUtc = licenseNow
                    });
                    await db.SaveChangesAsync(cancellationToken);
                }

                var customerConfiguration = customerFactory.Services.GetRequiredService<IConfiguration>();
                Assert.Equal(customerConnection, customerConfiguration.GetConnectionString("ConnectionString"));
                var customerApplicationSettings = customerFactory.Services
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<CustomerServer::Infrastructure.Settings.ApplicationSettings>>()
                    .Value;
                Assert.Equal(customerInfrastructureUsername, customerApplicationSettings.MasterUsername);
                Assert.Equal(customerInfrastructurePasswordHash, customerApplicationSettings.MasterPassword);
                Assert.Equal(
                    CustomerSecurity.PasswordVerificationOutcome.Success,
                    new CustomerSecurity.PasswordHashService().Verify(
                        customerInfrastructurePasswordHash, customerInfrastructurePassword));

                await using (var customerScope = customerFactory.Services.CreateAsyncScope())
                {
                    var db = customerScope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();
                    await db.Database.MigrateAsync(cancellationToken);
                }

                using var requesterClient = vendorFactory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    HandleCookies = true
                });
                using var reviewerClient = vendorFactory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    HandleCookies = true
                });

                var requesterToken = await SignInVendorAdminAsync(
                    vendorFactory, requesterClient, requesterName, requesterPassword, cancellationToken);
                var reviewerToken = await SignInVendorAdminAsync(
                    vendorFactory, reviewerClient, reviewerName, reviewerPassword, cancellationToken);

                Guid customerId;
                Guid installationId;
                Guid licenseId;
                await using (var scope = vendorFactory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                    var customer = await db.Customers.SingleAsync(
                        item => item.CustomerNumber == customerReference, cancellationToken);
                    var installation = await db.Installations.SingleAsync(
                        item => item.InstallationCode == installationCode, cancellationToken);
                    customerId = customer.Id;
                    installationId = installation.Id;
                    licenseId = await db.Licenses.Where(item => item.CustomerId == customer.Id)
                        .Select(item => item.Id).SingleAsync(cancellationToken);
                }

                using (var eligibleLicensesResponse = await requesterClient.GetAsync(
                    $"/api/vendor/provisioning-requests/eligible-licenses?customerId={customerId:D}&installationId={installationId:D}",
                    cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.OK, eligibleLicensesResponse.StatusCode);
                    using var eligibleLicensesJson = JsonDocument.Parse(
                        await eligibleLicensesResponse.Content.ReadAsStringAsync(cancellationToken));
                    var eligibleLicenses = eligibleLicensesJson.RootElement.GetProperty("items");
                    Assert.Contains(eligibleLicenses.EnumerateArray(), item =>
                        item.GetProperty("licenseId").GetGuid() == licenseId &&
                        item.GetProperty("sequence").GetInt32() == 1 &&
                        item.GetProperty("subsystems").GetArrayLength() == 2);
                }

                var requestId = Guid.NewGuid();
                using var createRequest = await PostWithTokenAsync(
                    requesterClient,
                    "/api/vendor/provisioning-requests/",
                    new
                    {
                        requestId,
                        customerId,
                        installationId,
                        licenseId,
                        companyName,
                        companyType = "independent",
                        initialAdminUserName = adminUserName,
                        initialAdminEmail = "admin-" + Guid.NewGuid().ToString("N")[..8] + "@example.invalid",
                        subsystems = new[] { "finance", "sales" },
                        fiscalPeriodTitle = "سال مالی آزمون",
                        fiscalPeriodStartDate = new DateOnly(2026, 3, 21),
                        fiscalPeriodEndDate = new DateOnly(2027, 3, 20),
                        parentCompanyReference = (string?)null
                    },
                    requesterToken,
                    cancellationToken);
                Assert.Equal(HttpStatusCode.Created, createRequest.StatusCode);
                using var createdJson = JsonDocument.Parse(
                    await createRequest.Content.ReadAsStringAsync(cancellationToken));
                var provisioningRecordId = createdJson.RootElement.GetProperty("id").GetGuid();
                var requestRowVersion = createdJson.RootElement.GetProperty("rowVersion").GetString()!;
                Assert.Equal("AwaitingSigner", createdJson.RootElement.GetProperty("status").GetString());

                using var approvalRequest = await PostWithTokenAsync(
                    requesterClient,
                    $"/api/vendor/provisioning-requests/{provisioningRecordId:D}/approval-request",
                    new { rowVersion = requestRowVersion },
                    requesterToken,
                    cancellationToken);
                Assert.Equal(HttpStatusCode.Accepted, approvalRequest.StatusCode);
                using var approvalJson = JsonDocument.Parse(
                    await approvalRequest.Content.ReadAsStringAsync(cancellationToken));
                var approvalId = approvalJson.RootElement.GetProperty("id").GetGuid();
                var approvalRowVersion = approvalJson.RootElement.GetProperty("rowVersion").GetString()!;

                using var decision = await PostWithTokenAsync(
                    reviewerClient,
                    $"/api/vendor/approvals/{approvalId:D}/decision",
                    new
                    {
                        decision = "Approved",
                        rowVersion = approvalRowVersion,
                        comment = "Independent reviewer approved the signed customer package."
                    },
                    reviewerToken,
                    cancellationToken);
                Assert.Equal(HttpStatusCode.OK, decision.StatusCode);

                await using (var scope = vendorFactory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                    var request = await db.ProvisioningRequests.SingleAsync(
                        item => item.Id == provisioningRecordId, cancellationToken);
                    Assert.Equal("PendingDelivery", request.Status);
                    Assert.Equal(DualHostVendorKeyId, request.KeyId);
                    Assert.False(string.IsNullOrWhiteSpace(request.SignatureBase64));
                }

                using var packageResponse = await PostWithTokenAsync(
                    reviewerClient,
                    $"/api/vendor/provisioning-requests/{provisioningRecordId:D}/package/export",
                    new { },
                    reviewerToken,
                    cancellationToken);
                Assert.Equal(HttpStatusCode.OK, packageResponse.StatusCode);
                var packageResponseText = await packageResponse.Content.ReadAsStringAsync(cancellationToken);
                using (var packageDocument = JsonDocument.Parse(packageResponseText))
                {
                    var payloadJson = packageDocument.RootElement.GetProperty("payloadJson").GetString();
                    Assert.False(string.IsNullOrWhiteSpace(payloadJson));
                    using var payloadDocument = JsonDocument.Parse(payloadJson!);
                    var payload = payloadDocument.RootElement;
                    Assert.Equal(4, payload.GetProperty("schemaVersion").GetInt32());
                    Assert.Equal(licenseId.ToString("D"), payload.GetProperty("licenseId").GetString());
                    Assert.Equal(1, payload.GetProperty("sequence").GetInt32());
                    Assert.Equal(new[] { "finance", "sales" },
                        payload.GetProperty("subsystems").EnumerateArray().Select(item => item.GetString()).ToArray());
                }

                Directory.CreateDirectory(exchangeDirectory);
                var packageFile = Path.Combine(exchangeDirectory, "customer-provisioning-package.json");
                await File.WriteAllTextAsync(packageFile, packageResponseText, Encoding.UTF8, cancellationToken);

                // The already-exported file remains verifiable offline, but Vendor must refuse
                // to export it again after a newer signed license sequence has been issued.
                await using (var scope = vendorFactory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                    var license = await db.Licenses.SingleAsync(item => item.Id == licenseId, cancellationToken);
                    var installation = await db.Installations.SingleAsync(item => item.Id == installationId, cancellationToken);
                    var revisionNow = DateTimeOffset.UtcNow;
                    var revisionPayload = new LicensePayload(
                        1, license.Id.ToString("D"), installation.Id.ToString("D"),
                        license.ProductCode, license.LicenseKind, revisionNow,
                        license.ValidFromUtc, license.ValidUntilUtc, license.GraceDays, 2,
                        new LicenseLimits(license.MaxCompanies, ["finance", "sales"]));
                    var revisionPayloadJson = CanonicalJson.SerializePayload(revisionPayload);
                    var revisionSignature = licenseKey.SignData(
                        Encoding.UTF8.GetBytes(revisionPayloadJson),
                        HashAlgorithmName.SHA256,
                        DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                    var revisionEnvelope = new LicenseEnvelope(
                        1, DualHostLicenseKeyId, LicenseAlgorithms.EcdsaP256Sha256,
                        revisionPayloadJson, Convert.ToBase64String(revisionSignature));
                    db.SignedLicenseEnvelopes.Add(new VendorSignedLicenseEnvelope
                    {
                        Id = Guid.NewGuid(),
                        LicenseId = license.Id,
                        Sequence = 2,
                        KeyId = revisionEnvelope.Kid,
                        Algorithm = revisionEnvelope.Algorithm,
                        EnvelopeJson = JsonSerializer.Serialize(revisionEnvelope),
                        PayloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(revisionPayloadJson))).ToLowerInvariant(),
                        CreatedAtUtc = revisionNow
                    });
                    license.LastIssuedSequence = 2;
                    await db.SaveChangesAsync(cancellationToken);
                }

                using (var staleReExport = await PostWithTokenAsync(
                    reviewerClient,
                    $"/api/vendor/provisioning-requests/{provisioningRecordId:D}/package/export",
                    new { },
                    reviewerToken,
                    cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.Conflict, staleReExport.StatusCode);
                    using var staleJson = JsonDocument.Parse(
                        await staleReExport.Content.ReadAsStringAsync(cancellationToken));
                    Assert.Equal("provisioning_license_sequence_stale",
                        staleJson.RootElement.GetProperty("error").GetString());
                }

                using var customerClientA = customerFactory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    HandleCookies = true,
                    AllowAutoRedirect = false
                });
                using var customerClientB = customerFactory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    HandleCookies = true,
                    AllowAutoRedirect = false
                });
                using (var infrastructureLoginA = await PostCustomerLoginAsync(
                    customerClientA, customerInfrastructureUsername, customerInfrastructurePassword, cancellationToken))
                {
                    var loginBodyA = await infrastructureLoginA.Content.ReadAsStringAsync(cancellationToken);
                    Assert.True(
                        infrastructureLoginA.StatusCode == HttpStatusCode.Redirect,
                        $"Customer infrastructure login failed: {(int)infrastructureLoginA.StatusCode}; location={infrastructureLoginA.Headers.Location}; body={loginBodyA}");
                    Assert.Equal("/", infrastructureLoginA.Headers.Location?.ToString());
                    Assert.Contains(
                        infrastructureLoginA.Headers.TryGetValues("Set-Cookie", out var cookiesA)
                            ? cookiesA
                            : Array.Empty<string>(),
                        value => value.StartsWith(".Faranou.Authentication=", StringComparison.Ordinal));
                }
                using (var infrastructureLoginB = await PostCustomerLoginAsync(
                    customerClientB, customerInfrastructureUsername, customerInfrastructurePassword, cancellationToken))
                {
                    var loginBodyB = await infrastructureLoginB.Content.ReadAsStringAsync(cancellationToken);
                    Assert.True(
                        infrastructureLoginB.StatusCode == HttpStatusCode.Redirect,
                        $"Customer infrastructure login failed: {(int)infrastructureLoginB.StatusCode}; location={infrastructureLoginB.Headers.Location}; body={loginBodyB}");
                    Assert.Equal("/", infrastructureLoginB.Headers.Location?.ToString());
                    Assert.Contains(
                        infrastructureLoginB.Headers.TryGetValues("Set-Cookie", out var cookiesB)
                            ? cookiesB
                            : Array.Empty<string>(),
                        value => value.StartsWith(".Faranou.Authentication=", StringComparison.Ordinal));
                }
                var customerTokenA = await GetCustomerAntiforgeryTokenAsync(customerClientA, cancellationToken);
                var customerTokenB = await GetCustomerAntiforgeryTokenAsync(customerClientB, cancellationToken);

                var packageFromOfflineFile = await File.ReadAllTextAsync(
                    packageFile, Encoding.UTF8, cancellationToken);

                // Simulate an interrupted removable-media/file copy. A truncated envelope
                // must be rejected without a partial company or claim; the intact file is retried below.
                var interruptedPackageFile = Path.Combine(exchangeDirectory, "customer-provisioning-package.partial");
                await File.WriteAllTextAsync(
                    interruptedPackageFile,
                    packageFromOfflineFile[..(packageFromOfflineFile.Length / 2)],
                    Encoding.UTF8,
                    cancellationToken);
                using (var interruptedTransfer = await PostCustomerJsonAsync(
                    customerClientA,
                    "/api/v1/provisioning/apply",
                    await File.ReadAllTextAsync(interruptedPackageFile, Encoding.UTF8, cancellationToken),
                    customerTokenA,
                    cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.BadRequest, interruptedTransfer.StatusCode);
                }

                // Export is a transport operation, not customer confirmation. A lost/ignored
                // acknowledgement must leave the Vendor request in PendingDelivery.
                await using (var pendingScope = vendorFactory.Services.CreateAsyncScope())
                {
                    var db = pendingScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                    Assert.Equal("PendingDelivery", (await db.ProvisioningRequests.SingleAsync(
                        item => item.Id == provisioningRecordId, cancellationToken)).Status);
                }

                var unknownKeyPackage = JsonNode.Parse(packageFromOfflineFile)!.AsObject();
                unknownKeyPackage["keyId"] = "not-in-customer-trust-store";
                using (var unknownKeyResponse = await PostCustomerJsonAsync(
                    customerClientA,
                    "/api/v1/provisioning/apply",
                    unknownKeyPackage.ToJsonString(),
                    customerTokenA,
                    cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.BadRequest, unknownKeyResponse.StatusCode);
                }

                var tamperedPackage = JsonNode.Parse(packageFromOfflineFile)!.AsObject();
                var payloadNode = JsonNode.Parse(tamperedPackage["payloadJson"]!.GetValue<string>())!.AsObject();
                payloadNode["companyName"] = "Tampered company name";
                tamperedPackage["payloadJson"] = payloadNode.ToJsonString();
                using (var tamperedResponse = await PostCustomerJsonAsync(
                    customerClientA,
                    "/api/v1/provisioning/apply",
                    tamperedPackage.ToJsonString(),
                    customerTokenA,
                    cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.BadRequest, tamperedResponse.StatusCode);
                }

                await using (var scope = customerFactory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();
                    Assert.Empty(await db.Companies.ToListAsync(cancellationToken));
                    Assert.Empty(await db.ProvisioningRequestClaims.ToListAsync(cancellationToken));
                }

                // The package is now physically read from the exchange directory by the Customer host.
                // Two independent HTTP clients race the same offline package through separate requests.
                var applyTasks = new[]
                {
                    PostCustomerJsonAsync(customerClientA, "/api/v1/provisioning/apply",
                        packageFromOfflineFile, customerTokenA, cancellationToken),
                    PostCustomerJsonAsync(customerClientB, "/api/v1/provisioning/apply",
                        packageFromOfflineFile, customerTokenB, cancellationToken)
                };
                using var applyResponseA = await applyTasks[0];
                using var applyResponseB = await applyTasks[1];
                Assert.Equal(HttpStatusCode.OK, applyResponseA.StatusCode);
                Assert.Equal(HttpStatusCode.OK, applyResponseB.StatusCode);

                var applyBodyA = await applyResponseA.Content.ReadAsStringAsync(cancellationToken);
                var applyBodyB = await applyResponseB.Content.ReadAsStringAsync(cancellationToken);
                using var applyJsonA = JsonDocument.Parse(applyBodyA);
                using var applyJsonB = JsonDocument.Parse(applyBodyB);
                var replayFlags = new[]
                {
                    applyJsonA.RootElement.GetProperty("replayed").GetBoolean(),
                    applyJsonB.RootElement.GetProperty("replayed").GetBoolean()
                };
                Assert.Contains(false, replayFlags);
                Assert.Contains(true, replayFlags);

                var enrollmentTokens = new[]
                {
                    ReadOptionalString(applyJsonA.RootElement, "enrollmentToken"),
                    ReadOptionalString(applyJsonB.RootElement, "enrollmentToken")
                }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
                var enrollmentToken = Assert.Single(enrollmentTokens)!;
                var acknowledgementJson = applyJsonA.RootElement.GetProperty("acknowledgement").GetRawText();
                Assert.Equal(
                    acknowledgementJson,
                    applyJsonB.RootElement.GetProperty("acknowledgement").GetRawText());

                await using (var scope = customerFactory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();
                    Assert.Equal(1, await db.Companies.CountAsync(item => item.Name == companyName, cancellationToken));
                    Assert.Equal(1, await db.Users.CountAsync(item => item.Username == adminUserName, cancellationToken));
                    var claim = await db.ProvisioningRequestClaims.SingleAsync(
                        item => item.RequestId == requestId, cancellationToken);
                    Assert.True(claim.MustChangePassword);
                    Assert.Null(claim.EnrollmentConsumedAtUtc);
                    Assert.NotEqual(enrollmentToken, claim.EnrollmentTokenHash);

                    var admin = await db.Users.SingleAsync(item => item.Username == adminUserName, cancellationToken);
                }

                // Even if an out-of-band operator sets a known credential, the real Razor login
                // endpoint must refuse to issue a session while the one-time enrollment is pending.
                var preEnrollmentPassword = "Known-Before-Enrollment-" + Guid.NewGuid().ToString("N");
                await using (var scope = customerFactory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();
                    var admin = await db.Users.SingleAsync(item => item.Username == adminUserName, cancellationToken);
                    admin.Password = new CustomerSecurity.PasswordHashService().Hash(preEnrollmentPassword);
                    await db.SaveChangesAsync(cancellationToken);
                }

                using var customerLoginClient = customerFactory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    HandleCookies = true,
                    AllowAutoRedirect = false
                });
                using (var deniedLogin = await PostCustomerLoginAsync(
                    customerLoginClient, adminUserName, preEnrollmentPassword, cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.OK, deniedLogin.StatusCode);
                    var cookies = deniedLogin.Headers.TryGetValues("Set-Cookie", out var deniedCookies)
                        ? deniedCookies.ToArray()
                        : Array.Empty<string>();
                    Assert.DoesNotContain(cookies, value =>
                        value.StartsWith(".Faranou.Authentication=", StringComparison.Ordinal));
                }

                var enrollmentTokenForPost = await GetCustomerAntiforgeryTokenAsync(customerClientA, cancellationToken);
                using (var enrollmentResponse = await PostCustomerJsonAsync(
                    customerClientA,
                    "/api/v1/provisioning/enrollment/complete",
                    JsonSerializer.Serialize(new
                    {
                        username = adminUserName,
                        enrollmentToken,
                        newPassword = adminPassword
                    }),
                    enrollmentTokenForPost,
                    cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.NoContent, enrollmentResponse.StatusCode);
                }

                await using (var scope = customerFactory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();
                    var claim = await db.ProvisioningRequestClaims.SingleAsync(
                        item => item.RequestId == requestId, cancellationToken);
                    Assert.False(claim.MustChangePassword);
                    Assert.NotNull(claim.EnrollmentConsumedAtUtc);
                    var authentication = scope.ServiceProvider.GetRequiredService<CustomerSecurity.AuthenticationService>();
                    var accepted = await authentication.AuthenticateAsync(adminUserName, adminPassword, "127.0.0.1");
                    Assert.Equal(CustomerSecurity.LoginOutcome.Succeeded, accepted.Outcome);
                }

                using (var successfulLogin = await PostCustomerLoginAsync(
                    customerLoginClient, adminUserName, adminPassword, cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.Redirect, successfulLogin.StatusCode);
                    Assert.StartsWith("/app/", successfulLogin.Headers.Location?.ToString());
                    var cookies = successfulLogin.Headers.TryGetValues("Set-Cookie", out var successCookies)
                        ? successCookies.ToArray()
                        : Array.Empty<string>();
                    Assert.Contains(cookies, value =>
                        value.StartsWith(".Faranou.Authentication=", StringComparison.Ordinal));
                }

                using (var repeatedEnrollment = await PostCustomerJsonAsync(
                    customerClientA,
                    "/api/v1/provisioning/enrollment/complete",
                    JsonSerializer.Serialize(new
                    {
                        username = adminUserName,
                        enrollmentToken,
                        newPassword = adminPassword
                    }),
                    enrollmentTokenForPost,
                    cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.BadRequest, repeatedEnrollment.StatusCode);
                }

                // A package retry returns the original signed acknowledgement without creating another company/admin.
                using (var packageReplay = await PostCustomerJsonAsync(
                    customerClientA,
                    "/api/v1/provisioning/apply",
                    packageFromOfflineFile,
                    customerTokenA,
                    cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.OK, packageReplay.StatusCode);
                    using var replayJson = JsonDocument.Parse(
                        await packageReplay.Content.ReadAsStringAsync(cancellationToken));
                    Assert.True(replayJson.RootElement.GetProperty("replayed").GetBoolean());
                    Assert.Equal(JsonValueKind.Null, replayJson.RootElement.GetProperty("enrollmentToken").ValueKind);
                    acknowledgementJson = replayJson.RootElement.GetProperty("acknowledgement").GetRawText();
                }

                var acknowledgementFile = Path.Combine(exchangeDirectory, "customer-acknowledgement.json");
                await File.WriteAllTextAsync(acknowledgementFile, acknowledgementJson, Encoding.UTF8, cancellationToken);
                var acknowledgementFromOfflineFile = await File.ReadAllTextAsync(
                    acknowledgementFile, Encoding.UTF8, cancellationToken);

                var tamperedAcknowledgement = JsonNode.Parse(acknowledgementFromOfflineFile)!.AsObject();
                var signature = tamperedAcknowledgement["signatureBase64"]!.GetValue<string>();
                tamperedAcknowledgement["signatureBase64"] =
                    (signature[0] == 'A' ? "B" : "A") + signature[1..];
                using (var invalidAcknowledgement = await vendorClientPostAcknowledgementAsync(
                    vendorFactory, requestId, tamperedAcknowledgement.ToJsonString(), cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.BadRequest, invalidAcknowledgement.StatusCode);
                }

                await using (var scope = vendorFactory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                    Assert.Equal("PendingDelivery", (await db.ProvisioningRequests.SingleAsync(
                        item => item.Id == provisioningRecordId, cancellationToken)).Status);
                }

                using (var acknowledgementResponse = await vendorClientPostAcknowledgementAsync(
                    vendorFactory, requestId, acknowledgementFromOfflineFile, cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.OK, acknowledgementResponse.StatusCode);
                    using var json = JsonDocument.Parse(
                        await acknowledgementResponse.Content.ReadAsStringAsync(cancellationToken));
                    Assert.Equal("Confirmed", json.RootElement.GetProperty("status").GetString());
                    Assert.False(json.RootElement.GetProperty("repeated").GetBoolean());
                }

                using (var exactAcknowledgementRetry = await vendorClientPostAcknowledgementAsync(
                    vendorFactory, requestId, acknowledgementFromOfflineFile, cancellationToken))
                {
                    Assert.Equal(HttpStatusCode.OK, exactAcknowledgementRetry.StatusCode);
                    using var json = JsonDocument.Parse(
                        await exactAcknowledgementRetry.Content.ReadAsStringAsync(cancellationToken));
                    Assert.Equal("Confirmed", json.RootElement.GetProperty("status").GetString());
                    Assert.True(json.RootElement.GetProperty("repeated").GetBoolean());
                }

                await using (var scope = vendorFactory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                    var request = await db.ProvisioningRequests.SingleAsync(
                        item => item.Id == provisioningRecordId, cancellationToken);
                    Assert.Equal("Confirmed", request.Status);
                    Assert.NotNull(request.ConfirmedAtUtc);
                    Assert.Equal("Applied", request.AcknowledgementResult);
                    Assert.NotNull(request.AcknowledgementSha256);
                    Assert.Equal(1, await db.AuditEvents.CountAsync(
                        item => item.ActionCode == "vendor.provisioning.acknowledgement-confirmed" &&
                                item.SubjectId == provisioningRecordId.ToString("D"),
                        cancellationToken));
                }
            }
            finally
            {
                await DropDatabaseAsync(customerBaseConnection, customerDatabaseName, CancellationToken.None);
            }
        }
        finally
        {
            await DropDatabaseAsync(vendorBaseConnection, vendorDatabaseName, CancellationToken.None);
            if (Directory.Exists(exchangeDirectory))
                Directory.Delete(exchangeDirectory, recursive: true);
        }
    }

    private static async Task<string> SignInVendorAdminAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var (antiForgeryToken, _) = await GetAntiforgeryTokenAsync(client, cancellationToken);
        using (var passwordResponse = await PostWithTokenAsync(
            client, "/api/auth/password", new { userName, password }, antiForgeryToken, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, passwordResponse.StatusCode);
            using var json = JsonDocument.Parse(await passwordResponse.Content.ReadAsStringAsync(cancellationToken));
            Assert.True(json.RootElement.GetProperty("requiresSecondFactor").GetBoolean());
        }

        string authenticatorKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
            var user = await users.FindByNameAsync(userName);
            Assert.NotNull(user);
            authenticatorKey = (await users.GetAuthenticatorKeyAsync(user!))!;
        }

        using (var verifyResponse = await PostWithTokenAsync(
            client,
            "/api/auth/totp/verify",
            new { code = GenerateTotpCode(authenticatorKey) },
            antiForgeryToken,
            cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        }

        return (await GetAntiforgeryTokenAsync(client, cancellationToken)).Token;
    }

    private static async Task<HttpResponseMessage> PostCustomerLoginAsync(
        HttpClient client,
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        using var page = await client.GetAsync("/Account/Login", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(cancellationToken);
        var tokenMatch = Regex.Match(
            html,
            @"<input(?=[^>]*\bname=""__RequestVerificationToken"")(?=[^>]*\bvalue=""([^""]+)"")[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success, "The login page must emit an antiforgery form token.");
        var requestToken = System.Net.WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);

        return await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = requestToken,
                ["ViewModel.Username"] = userName,
                ["ViewModel.Password"] = password,
                ["ViewModel.RememberMe"] = "false",
                ["ReturnUrl"] = string.Empty
            }),
            cancellationToken);
    }

    private static async Task<string> GetCustomerAntiforgeryTokenAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            "/api/v1/provisioning/enrollment/antiforgery", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var token = json.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private static async Task<HttpResponseMessage> PostCustomerJsonAsync(
        HttpClient client,
        string path,
        string json,
        string antiForgeryToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", antiForgeryToken);
        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<HttpResponseMessage> vendorClientPostAcknowledgementAsync(
        WebApplicationFactory<Program> factory,
        Guid requestId,
        string acknowledgementJson,
        CancellationToken cancellationToken)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/vendor/provisioning-requests/{requestId:D}/acknowledgement")
        {
            Content = new StringContent(acknowledgementJson, Encoding.UTF8, "application/json")
        };
        return await client.SendAsync(request, cancellationToken);
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string CreateE2ETestPassword() =>
        "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "z";

    private sealed class DualHostProvisioningSigner(ECDsa key) : IProvisioningSigningProvider
    {
        public ValueTask<LicenseSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalPayload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var signature = key.SignData(
                canonicalPayload.Span,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return ValueTask.FromResult(LicenseSigningResult.Success(
                DualHostVendorKeyId,
                LicenseAlgorithms.EcdsaP256Sha256,
                signature));
        }
    }

    private sealed class DualHostAcknowledgementSigner(ECDsa key)
        : CustomerProvisioning.ICustomerProvisioningAcknowledgementSigningProvider
    {
        public Task<CustomerProvisioning.CustomerProvisioningAcknowledgementSigningResult> SignAsync(
            ReadOnlyMemory<byte> canonicalAcknowledgementUtf8,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var signature = key.SignData(
                canonicalAcknowledgementUtf8.Span,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return Task.FromResult(new CustomerProvisioning.CustomerProvisioningAcknowledgementSigningResult(
                true,
                DualHostAcknowledgementKeyId,
                Convert.ToBase64String(signature),
                null));
        }
    }

}
