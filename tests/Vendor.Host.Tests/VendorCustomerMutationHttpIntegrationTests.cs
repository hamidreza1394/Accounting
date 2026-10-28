using System.Buffers.Binary;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Json;
using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence;
using Faranou.Vendor.Persistence.Domain;
using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>
/// A narrow end-to-end mutation test: real Vendor SQL Server, Identity MFA cookie,
/// production permission/resource handlers, anti-forgery, rowversion and audit writer.
/// </summary>
public sealed class VendorCustomerMutationHttpIntegrationTests
{
    [Fact]
    public async Task CustomerUpdateEnforcesAntiforgeryAndRowVersionAndAuditsOnlyCommittedWrites()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var reviewerUserName = string.Empty;
        var reviewerAuthenticatorKey = string.Empty;
        var customerApprovalId = Guid.Empty;
        var customerApprovalRowVersion = string.Empty;
        var installationApprovalId = Guid.Empty;
        var installationApprovalRowVersion = string.Empty;
        var licenseApprovalId = Guid.Empty;
        var licenseApprovalRowVersion = string.Empty;
        var baseConnection = Environment.GetEnvironmentVariable("FARANO_VENDOR_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("FARANO_REQUIRE_VENDOR_SQLSERVER"), "1", StringComparison.Ordinal))
                Assert.Fail("CI requires FARANO_VENDOR_TEST_SQLSERVER for Vendor mutation integration tests.");
            Assert.Skip("A disposable SQL Server connection is required for Vendor mutation integration tests.");
            return;
        }

        var databaseName = "FaranouVendorMutation_" + Guid.NewGuid().ToString("N");
        var testConnection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = databaseName }.ConnectionString;
        await CreateDatabaseAsync(baseConnection, databaseName, cancellationToken);

        try
        {
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
            {
                webHost.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:VendorDb"] = testConnection
                    }));
                webHost.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IVendorAuditWriter>();
                    services.AddSingleton<AuditFailureSwitch>();
                    services.AddSingleton<CustomerInsertRaceGate>();
                    services.ConfigureDbContext<VendorDbContext>((provider, options) =>
                        options.AddInterceptors(provider.GetRequiredService<CustomerInsertRaceGate>()));
                    services.AddScoped<IVendorAuditWriter>(provider => new SwitchableVendorAuditWriter(
                        provider.GetRequiredService<VendorDbContext>(),
                        provider.GetRequiredService<TimeProvider>(),
                        provider.GetRequiredService<AuditFailureSwitch>()));
                });
            });

            await using (var setupScope = factory.Services.CreateAsyncScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                await db.Database.MigrateAsync(cancellationToken);

                var users = setupScope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
                var roles = setupScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                var role = new IdentityRole<Guid>("customer-mutation-test") { Id = Guid.NewGuid() };
                var roleResult = await roles.CreateAsync(role);
                Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Code)));
                foreach (var permission in new[]
                {
                    VendorPermissionCatalog.CustomersRead,
                    VendorPermissionCatalog.CustomersManage,
                    VendorPermissionCatalog.InstallationsRead,
                    VendorPermissionCatalog.InstallationsManage,
                    VendorPermissionCatalog.ContractsRead,
                    VendorPermissionCatalog.ContractsManage,
                    VendorPermissionCatalog.LicensesRead,
                    VendorPermissionCatalog.LicensesManage,
                    VendorPermissionCatalog.LicensesIssue,
                    VendorPermissionCatalog.ApprovalsRead,
                    VendorPermissionCatalog.ApprovalsRequest,
                    VendorPermissionCatalog.ApprovalsReview
                })
                {
                    var permissionResult = await roles.AddClaimAsync(role,
                        new Claim(VendorPermissionCatalog.ClaimType, permission));
                    Assert.True(permissionResult.Succeeded, string.Join("; ", permissionResult.Errors.Select(error => error.Code)));
                }

                var user = new VendorAdminUser
                {
                    UserName = "mutation-" + Guid.NewGuid().ToString("N"),
                    Email = Guid.NewGuid().ToString("N") + "@example.invalid",
                    EmailConfirmed = true,
                    IsEnabled = true,
                    MustChangePassword = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                var userResult = await users.CreateAsync(user, "Strong-Vendor#2026-Password");
                Assert.True(userResult.Succeeded, string.Join("; ", userResult.Errors.Select(error => error.Code)));
                var addRoleResult = await users.AddToRoleAsync(user, role.Name!);
                Assert.True(addRoleResult.Succeeded, string.Join("; ", addRoleResult.Errors.Select(error => error.Code)));
                var keyResult = await users.ResetAuthenticatorKeyAsync(user);
                Assert.True(keyResult.Succeeded);
                var mfaResult = await users.SetTwoFactorEnabledAsync(user, true);
                Assert.True(mfaResult.Succeeded);

                var reviewer = new VendorAdminUser
                {
                    UserName = "reviewer-" + Guid.NewGuid().ToString("N"),
                    Email = Guid.NewGuid().ToString("N") + "@example.invalid",
                    EmailConfirmed = true,
                    IsEnabled = true,
                    MustChangePassword = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                var reviewerResult = await users.CreateAsync(reviewer, "Strong-Vendor#2026-Reviewer");
                Assert.True(reviewerResult.Succeeded, string.Join("; ", reviewerResult.Errors.Select(error => error.Code)));
                var reviewerRoleResult = await users.AddToRoleAsync(reviewer, role.Name!);
                Assert.True(reviewerRoleResult.Succeeded, string.Join("; ", reviewerRoleResult.Errors.Select(error => error.Code)));
                var reviewerKeyResult = await users.ResetAuthenticatorKeyAsync(reviewer);
                Assert.True(reviewerKeyResult.Succeeded);
                var reviewerMfaResult = await users.SetTwoFactorEnabledAsync(reviewer, true);
                Assert.True(reviewerMfaResult.Succeeded);
                reviewerUserName = reviewer.UserName!;
                reviewerAuthenticatorKey = (await users.GetAuthenticatorKeyAsync(reviewer))!;

                var customer = new VendorCustomer
                {
                    Id = Guid.NewGuid(),
                    CustomerNumber = "IT-" + Guid.NewGuid().ToString("N")[..20],
                    LegalName = "Integration Test Customer",
                    DisplayName = "Before Update",
                    Status = "Active",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                var otherCustomer = new VendorCustomer
                {
                    Id = Guid.NewGuid(),
                    CustomerNumber = "OTHER-" + Guid.NewGuid().ToString("N")[..16],
                    LegalName = "Other Integration Customer",
                    DisplayName = "Other Customer",
                    Status = "Active",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                db.Customers.AddRange(customer, otherCustomer);
                await db.SaveChangesAsync(cancellationToken);

                var installation = new VendorInstallation
                {
                    Id = Guid.NewGuid(), CustomerId = customer.Id,
                    InstallationCode = "IT-INST-" + Guid.NewGuid().ToString("N")[..12],
                    Name = "Before Installation Update", Environment = "Production",
                    Status = "Active", CreatedAtUtc = DateTimeOffset.UtcNow
                };
                var otherInstallation = new VendorInstallation
                {
                    Id = Guid.NewGuid(), CustomerId = otherCustomer.Id,
                    InstallationCode = "OTHER-INST-" + Guid.NewGuid().ToString("N")[..12],
                    Name = "Other Installation", Environment = "Staging",
                    Status = "Active", CreatedAtUtc = DateTimeOffset.UtcNow
                };
                var contract = new VendorContract
                {
                    Id = Guid.NewGuid(), CustomerId = customer.Id,
                    ContractNumber = "IT-CON-" + Guid.NewGuid().ToString("N")[..12],
                    Status = "Draft", StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    CurrencyCode = "EUR", CreatedAtUtc = DateTimeOffset.UtcNow
                };
                var otherContract = new VendorContract
                {
                    Id = Guid.NewGuid(), CustomerId = otherCustomer.Id,
                    ContractNumber = "OTHER-CON-" + Guid.NewGuid().ToString("N")[..12],
                    Status = "Draft", StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    CurrencyCode = "EUR", CreatedAtUtc = DateTimeOffset.UtcNow
                };
                db.Installations.AddRange(installation, otherInstallation);
                db.Contracts.AddRange(contract, otherContract);
                await db.SaveChangesAsync(cancellationToken);

                var license = new VendorLicense
                {
                    Id = Guid.NewGuid(), CustomerId = customer.Id,
                    ContractId = contract.Id, InstallationId = installation.Id,
                    LicenseNumber = "IT-LIC-" + Guid.NewGuid().ToString("N")[..12],
                    ProductCode = "FARANOU-BASE", Status = "Draft",
                    ValidFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                var otherLicense = new VendorLicense
                {
                    Id = Guid.NewGuid(), CustomerId = otherCustomer.Id,
                    LicenseNumber = "OTHER-LIC-" + Guid.NewGuid().ToString("N")[..12],
                    ProductCode = "FARANOU-BASE", Status = "Draft",
                    ValidFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                db.Licenses.AddRange(license, otherLicense);
                await db.SaveChangesAsync(cancellationToken);

                // Read server-generated SQL Server rowversions after inserts.
                await db.Entry(customer).ReloadAsync(cancellationToken);
                await db.Entry(installation).ReloadAsync(cancellationToken);
                await db.Entry(contract).ReloadAsync(cancellationToken);
                await db.Entry(license).ReloadAsync(cancellationToken);
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });

            var (antiForgeryToken, _) = await GetAntiforgeryTokenAsync(client, cancellationToken);
            using (var passwordResponse = await PostWithTokenAsync(client, "/api/auth/password",
                new { userName = await GetTestUserNameAsync(factory, cancellationToken), password = "Strong-Vendor#2026-Password" },
                antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, passwordResponse.StatusCode);
                using var passwordJson = JsonDocument.Parse(await passwordResponse.Content.ReadAsStringAsync(cancellationToken));
                Assert.True(passwordJson.RootElement.GetProperty("requiresSecondFactor").GetBoolean());
            }

            string authenticatorKey;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<VendorAdminUser>>();
                var user = await users.FindByNameAsync(await GetTestUserNameAsync(factory, cancellationToken));
                Assert.NotNull(user);
                authenticatorKey = (await users.GetAuthenticatorKeyAsync(user!))!;
            }

            using (var verifyResponse = await PostWithTokenAsync(client, "/api/auth/totp/verify",
                new { code = GenerateTotpCode(authenticatorKey) }, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
            }

            // Antiforgery request tokens are bound to the current claims identity.
            // The anonymous token used for password/TOTP requests must not be reused
            // after the authenticated application cookie is issued.
            (antiForgeryToken, _) = await GetAntiforgeryTokenAsync(client, cancellationToken);

            Guid customerId;
            byte[] initialRowVersion;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var customer = await db.Customers.SingleAsync(item => item.CustomerNumber.StartsWith("IT-"), cancellationToken);
                customerId = customer.Id;
                initialRowVersion = customer.RowVersion.ToArray();
            }

            // Optional customerId filters must return only related records for dropdowns.
            foreach (var resource in new[] { "installations", "contracts", "licenses" })
            {
                using var response = await client.GetAsync(
                    $"/api/vendor/{resource}/?customerId={customerId:D}&page=1&pageSize=100", cancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var items = json.RootElement.GetProperty("items");
                Assert.Single(items.EnumerateArray());
                Assert.Equal(customerId.ToString("D"), items[0].GetProperty("customerId").GetString());
            }

            var path = $"/api/vendor/customers/{customerId:D}";
            var updateBody = new
            {
                rowVersion = Convert.ToBase64String(initialRowVersion),
                customerNumber = "IT-" + customerId.ToString("N")[..20],
                legalName = "Integration Test Customer",
                displayName = "After Update"
            };

            using (var missingTokenRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(updateBody)
            })
            using (var response = await client.SendAsync(missingTokenRequest, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            using (var invalidTokenRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(updateBody)
            })
            {
                invalidTokenRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", "not-a-valid-antiforgery-token");
                using var response = await client.SendAsync(invalidTokenRequest, cancellationToken);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            using (var malformedVersionRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(new
                {
                    rowVersion = "not-base64",
                    customerNumber = "IT-" + customerId.ToString("N")[..20],
                    legalName = "Integration Test Customer",
                    displayName = "Must Not Persist"
                })
            })
            {
                malformedVersionRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiForgeryToken);
                using var response = await client.SendAsync(malformedVersionRequest, cancellationToken);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("invalid_row_version", json.RootElement.GetProperty("error").GetString());
            }

            using (var validRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(updateBody)
            })
            {
                validRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiForgeryToken);
                using var response = await client.SendAsync(validRequest, cancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            using (var staleRequest = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(updateBody)
            })
            {
                staleRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiForgeryToken);
                using var response = await client.SendAsync(staleRequest, cancellationToken);
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("stale_row_version", json.RootElement.GetProperty("error").GetString());
            }

            await using (var verifyScope = factory.Services.CreateAsyncScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var persisted = await db.Customers.AsNoTracking().SingleAsync(item => item.Id == customerId, cancellationToken);
                Assert.Equal("After Update", persisted.DisplayName);
                Assert.NotEqual(initialRowVersion, persisted.RowVersion);
                Assert.Equal(1, await db.AuditEvents.CountAsync(item =>
                    item.ActionCode == "vendor.customer.updated" && item.SubjectId == customerId.ToString("D"), cancellationToken));
            }

            // Direct status changes are blocked; a distinct administrator must approve the proposal.
            byte[] customerStatusVersion;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                customerStatusVersion = (await db.Customers.AsNoTracking()
                    .SingleAsync(item => item.Id == customerId, cancellationToken)).RowVersion.ToArray();
            }

            using (var response = await PostWithTokenAsync(client, path + "/status",
                new { status = "Suspended", rowVersion = Convert.ToBase64String(customerStatusVersion) },
                antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("approval_required", json.RootElement.GetProperty("error").GetString());
            }

            using (var response = await PostWithTokenAsync(client, "/api/vendor/approvals/status-change",
                new
                {
                    resourceType = "customer",
                    resourceId = customerId,
                    targetStatus = "Draft",
                    rowVersion = Convert.ToBase64String(customerStatusVersion)
                }, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("invalid_status_transition", json.RootElement.GetProperty("error").GetString());
            }

            var approvalBody = new
            {
                resourceType = "customer",
                resourceId = customerId,
                targetStatus = "Suspended",
                rowVersion = Convert.ToBase64String(customerStatusVersion)
            };
            using (var response = await PostWithTokenAsync(client, "/api/vendor/approvals/status-change",
                approvalBody, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                customerApprovalId = json.RootElement.GetProperty("id").GetGuid();
                customerApprovalRowVersion = json.RootElement.GetProperty("rowVersion").GetString()!;
                Assert.Equal("Pending", json.RootElement.GetProperty("status").GetString());
            }

            using (var response = await client.GetAsync("/api/vendor/approvals/?status=Pending&page=1&pageSize=25", cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                // At this point only the customer status request has been created.
                // The license approval request is intentionally created later, after
                // the license metadata RowVersion has been refreshed.
                Assert.Equal(1, json.RootElement.GetProperty("totalCount").GetInt32());
                Assert.Contains(json.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == customerApprovalId);
            }

            using (var response = await PostWithTokenAsync(client, "/api/vendor/approvals/status-change",
                approvalBody, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("approval_already_pending", json.RootElement.GetProperty("error").GetString());
            }

            using (var response = await PostWithTokenAsync(client, $"/api/vendor/approvals/{customerApprovalId:D}/decision",
                new { decision = "Approved", rowVersion = customerApprovalRowVersion, comment = "self approval must fail" },
                antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var persisted = await db.Customers.AsNoTracking().SingleAsync(item => item.Id == customerId, cancellationToken);
                Assert.Equal("Active", persisted.Status);
                Assert.Equal(1, await db.ApprovalRequests.CountAsync(item =>
                    item.Id == customerApprovalId && item.Status == "Pending", cancellationToken));
                Assert.Equal(1, await db.AuditEvents.CountAsync(item =>
                    item.ActionCode == "vendor.approval.requested" && item.SubjectId == customerApprovalId.ToString("D"), cancellationToken));
            }

            // Installation update: real HTTP, current RowVersion succeeds, replay is rejected.
            Guid installationId;
            byte[] installationRowVersion;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var installation = await db.Installations.SingleAsync(item => item.InstallationCode.StartsWith("IT-INST-"), cancellationToken);
                installationId = installation.Id;
                installationRowVersion = installation.RowVersion.ToArray();
            }
            var installationPath = $"/api/vendor/installations/{installationId:D}";
            var installationBody = new
            {
                installationCode = "IT-INST-UPDATED-" + installationId.ToString("N")[..8],
                name = "After Installation Update",
                environment = "Staging",
                rowVersion = Convert.ToBase64String(installationRowVersion)
            };
            using (var response = await SendPutAsync(client, installationPath, installationBody, antiForgeryToken, cancellationToken))
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using (var response = await SendPutAsync(client, installationPath, installationBody, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("stale_row_version", json.RootElement.GetProperty("error").GetString());
            }

            // Direct installation status mutation is also blocked; submit a separate approval request.
            byte[] installationStatusVersion;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                installationStatusVersion = (await db.Installations.AsNoTracking()
                    .SingleAsync(item => item.Id == installationId, cancellationToken)).RowVersion.ToArray();
            }

            using (var response = await PostWithTokenAsync(client, installationPath + "/status",
                new { status = "Suspended", rowVersion = Convert.ToBase64String(installationStatusVersion) },
                antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("approval_required", json.RootElement.GetProperty("error").GetString());
            }

            using (var response = await PostWithTokenAsync(client, "/api/vendor/approvals/status-change",
                new
                {
                    resourceType = "installation",
                    resourceId = installationId,
                    targetStatus = "Suspended",
                    rowVersion = Convert.ToBase64String(installationStatusVersion)
                }, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                installationApprovalId = json.RootElement.GetProperty("id").GetGuid();
                installationApprovalRowVersion = json.RootElement.GetProperty("rowVersion").GetString()!;
            }

            // Contract update is allowed while Draft and enforces RowVersion on replay.
            Guid contractId;
            byte[] contractRowVersion;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var contract = await db.Contracts.SingleAsync(item => item.ContractNumber.StartsWith("IT-CON-"), cancellationToken);
                contractId = contract.Id;
                contractRowVersion = contract.RowVersion.ToArray();
            }
            var contractPath = $"/api/vendor/contracts/{contractId:D}";
            var contractBody = new
            {
                contractNumber = "IT-CON-UPDATED-" + contractId.ToString("N")[..8],
                startsAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
                endsAtUtc = (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(365),
                currencyCode = "EUR",
                rowVersion = Convert.ToBase64String(contractRowVersion)
            };
            using (var response = await SendPutAsync(client, contractPath, contractBody, antiForgeryToken, cancellationToken))
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using (var response = await SendPutAsync(client, contractPath, contractBody, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("stale_row_version", json.RootElement.GetProperty("error").GetString());
            }

            // License update must reject contract/installation references owned by another customer.
            Guid licenseId;
            byte[] licenseRowVersion;
            Guid otherContractId;
            Guid otherInstallationId;
            Guid validContractId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var license = await db.Licenses.SingleAsync(item => item.LicenseNumber.StartsWith("IT-LIC-"), cancellationToken);
                licenseId = license.Id;
                licenseRowVersion = license.RowVersion.ToArray();
                otherContractId = await db.Contracts.Where(item => item.ContractNumber.StartsWith("OTHER-CON-"))
                    .Select(item => item.Id).SingleAsync(cancellationToken);
                otherInstallationId = await db.Installations.Where(item => item.InstallationCode.StartsWith("OTHER-INST-"))
                    .Select(item => item.Id).SingleAsync(cancellationToken);
                validContractId = await db.Contracts.Where(item => item.ContractNumber.StartsWith("IT-CON-"))
                    .Select(item => item.Id).SingleAsync(cancellationToken);
            }
            var licensePath = $"/api/vendor/licenses/{licenseId:D}";
            var licenseBody = new
            {
                contractId = (Guid?)otherContractId,
                installationId = (Guid?)otherInstallationId,
                licenseNumber = "IT-LIC-UPDATED-" + licenseId.ToString("N")[..8],
                productCode = "FARANOU-PRO",
                licenseKind = "term",
                maxCompanies = 25,
                subsystems = new[] { "finance", "sales" },
                graceDays = 30,
                validFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
                validUntilUtc = (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(365),
                rowVersion = Convert.ToBase64String(licenseRowVersion)
            };
            using (var response = await SendPutAsync(client, licensePath, licenseBody, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("contract_customer_mismatch", json.RootElement.GetProperty("error").GetString());
            }

            // A valid contract does not make a foreign installation acceptable.
            var wrongInstallationLicenseBody = new
            {
                contractId = (Guid?)validContractId,
                installationId = (Guid?)otherInstallationId,
                licenseNumber = licenseBody.licenseNumber,
                productCode = licenseBody.productCode,
                licenseKind = licenseBody.licenseKind,
                maxCompanies = licenseBody.maxCompanies,
                subsystems = licenseBody.subsystems,
                graceDays = licenseBody.graceDays,
                validFromUtc = licenseBody.validFromUtc,
                validUntilUtc = licenseBody.validUntilUtc,
                rowVersion = Convert.ToBase64String(licenseRowVersion)
            };
            using (var response = await SendPutAsync(client, licensePath, wrongInstallationLicenseBody, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("installation_customer_mismatch", json.RootElement.GetProperty("error").GetString());
            }

            // Correct the relationships and prove the update succeeds, then reject replay of the old version.
            var validInstallationId = installationId;
            var validLicenseBody = new
            {
                contractId = (Guid?)validContractId,
                installationId = (Guid?)validInstallationId,
                licenseNumber = licenseBody.licenseNumber,
                productCode = licenseBody.productCode,
                licenseKind = licenseBody.licenseKind,
                maxCompanies = licenseBody.maxCompanies,
                subsystems = licenseBody.subsystems,
                graceDays = licenseBody.graceDays,
                validFromUtc = licenseBody.validFromUtc,
                validUntilUtc = licenseBody.validUntilUtc,
                rowVersion = Convert.ToBase64String(licenseRowVersion)
            };
            using (var response = await SendPutAsync(client, licensePath, validLicenseBody, antiForgeryToken, cancellationToken))
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using (var response = await SendPutAsync(client, licensePath, validLicenseBody, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("stale_row_version", json.RootElement.GetProperty("error").GetString());
            }

            Guid approvalLicenseId;
            byte[] licenseVersion;
            await using (var licenseScope = factory.Services.CreateAsyncScope())
            {
                var db = licenseScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var license = await db.Licenses.AsNoTracking()
                    .SingleAsync(item => item.LicenseNumber.StartsWith("IT-LIC-"), cancellationToken);
                approvalLicenseId = license.Id;
                licenseVersion = license.RowVersion.ToArray();
            }

            var licenseApprovalBody = new
            {
                action = "issue",
                rowVersion = Convert.ToBase64String(licenseVersion)
            };
            using (var response = await PostWithTokenAsync(client,
                $"/api/vendor/licenses/{approvalLicenseId:D}/approval-request",
                licenseApprovalBody, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                licenseApprovalId = json.RootElement.GetProperty("id").GetGuid();
                licenseApprovalRowVersion = json.RootElement.GetProperty("rowVersion").GetString()!;
                Assert.Equal("Pending", json.RootElement.GetProperty("status").GetString());
            }

            using (var response = await PostWithTokenAsync(client,
                $"/api/vendor/licenses/{approvalLicenseId:D}/approval-request",
                licenseApprovalBody, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("approval_already_pending", json.RootElement.GetProperty("error").GetString());
            }

            using (var response = await client.GetAsync("/api/vendor/approvals/?status=Pending&page=1&pageSize=25", cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                // Customer and installation requests are still pending; license adds the third.
                Assert.Equal(3, json.RootElement.GetProperty("totalCount").GetInt32());
                var pendingIds = json.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("id").GetGuid())
                    .ToHashSet();
                Assert.Contains(customerApprovalId, pendingIds);
                Assert.Contains(installationApprovalId, pendingIds);
                Assert.Contains(licenseApprovalId, pendingIds);
            }

            await using (var auditScope = factory.Services.CreateAsyncScope())
            {
                var db = auditScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                Assert.Equal(1, await db.AuditEvents.CountAsync(item =>
                    item.ActionCode == "vendor.installation.updated" && item.SubjectId == installationId.ToString("D"), cancellationToken));
                Assert.Equal(1, await db.AuditEvents.CountAsync(item =>
                    item.ActionCode == "vendor.contract.updated" && item.SubjectId == contractId.ToString("D"), cancellationToken));
                Assert.Equal(1, await db.AuditEvents.CountAsync(item =>
                    item.ActionCode == "vendor.license.metadata-updated" && item.SubjectId == licenseId.ToString("D"), cancellationToken));
            }

            // A failed audit append must abort the enclosing business transaction.
            byte[] rollbackRowVersion;
            int customerAuditCountBeforeFailure;
            await using (var rollbackScope = factory.Services.CreateAsyncScope())
            {
                var db = rollbackScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var currentCustomer = await db.Customers.AsNoTracking()
                    .SingleAsync(item => item.Id == customerId, cancellationToken);
                rollbackRowVersion = currentCustomer.RowVersion.ToArray();
                customerAuditCountBeforeFailure = await db.AuditEvents.CountAsync(item =>
                    item.ActionCode == "vendor.customer.updated" && item.SubjectId == customerId.ToString("D"), cancellationToken);
            }

            var auditFailureSwitch = factory.Services.GetRequiredService<AuditFailureSwitch>();
            auditFailureSwitch.FailNextAppend = true;
            var rollbackBody = new
            {
                rowVersion = Convert.ToBase64String(rollbackRowVersion),
                customerNumber = "IT-" + customerId.ToString("N")[..20],
                legalName = "Integration Test Customer",
                displayName = "Must Roll Back"
            };
            var failureObserved = false;
            try
            {
                using var response = await SendPutAsync(client, path, rollbackBody, antiForgeryToken, cancellationToken);
                failureObserved = response.StatusCode == HttpStatusCode.InternalServerError;
            }
            catch (HttpRequestException)
            {
                // TestServer may surface an unhandled endpoint exception directly to the client.
                failureObserved = true;
            }
            Assert.True(failureObserved, "The injected audit failure must fail the HTTP mutation.");

            await using (var verificationScope = factory.Services.CreateAsyncScope())
            {
                var db = verificationScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var persistedCustomer = await db.Customers.AsNoTracking()
                    .SingleAsync(item => item.Id == customerId, cancellationToken);
                Assert.Equal("After Update", persistedCustomer.DisplayName);
                Assert.Equal(customerAuditCountBeforeFailure, await db.AuditEvents.CountAsync(item =>
                    item.ActionCode == "vendor.customer.updated" && item.SubjectId == customerId.ToString("D"), cancellationToken));
            }

            // Force two authenticated HTTP creates past the preflight duplicate check and
            // into SQL Server INSERT concurrently. The unique index must arbitrate the race.
            var raceGate = factory.Services.GetRequiredService<CustomerInsertRaceGate>();
            var racedCustomerNumber = "RACE-" + Guid.NewGuid().ToString("N")[..20];
            var racedCustomerBody = new
            {
                customerNumber = racedCustomerNumber,
                legalName = "Unique Race Integration Customer",
                displayName = "Unique Race Integration Customer"
            };
            raceGate.Enable();
            var firstRaceResponseTask = PostWithTokenAsync(
                client, "/api/vendor/customers/", racedCustomerBody, antiForgeryToken, cancellationToken);
            var secondRaceResponseTask = PostWithTokenAsync(
                client, "/api/vendor/customers/", racedCustomerBody, antiForgeryToken, cancellationToken);
            using var firstRaceResponse = await firstRaceResponseTask;
            using var secondRaceResponse = await secondRaceResponseTask;
            raceGate.Disable();

            // The status assertions alone can pass through the preflight duplicate check.
            // Require proof that both INSERT commands reached the SQL Server execution gate.
            Assert.Equal(2, raceGate.ObservedInsertCount);

            var raceStatuses = new[] { firstRaceResponse.StatusCode, secondRaceResponse.StatusCode };
            Assert.Single(raceStatuses, status => status == HttpStatusCode.Created);
            Assert.Single(raceStatuses, status => status == HttpStatusCode.Conflict);
            var conflictResponse = firstRaceResponse.StatusCode == HttpStatusCode.Conflict
                ? firstRaceResponse
                : secondRaceResponse;
            var createdResponse = firstRaceResponse.StatusCode == HttpStatusCode.Created
                ? firstRaceResponse
                : secondRaceResponse;
            using (var conflictJson = JsonDocument.Parse(await conflictResponse.Content.ReadAsStringAsync(cancellationToken)))
                Assert.Equal("customer_number_exists", conflictJson.RootElement.GetProperty("error").GetString());
            using var createdJson = JsonDocument.Parse(await createdResponse.Content.ReadAsStringAsync(cancellationToken));
            var createdCustomerId = createdJson.RootElement.GetProperty("id").GetGuid();

            // A distinct MFA-authenticated administrator can approve both requests.
            using var reviewerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });
            var (reviewerAntiForgeryToken, _) = await GetAntiforgeryTokenAsync(reviewerClient, cancellationToken);
            using (var passwordResponse = await PostWithTokenAsync(reviewerClient, "/api/auth/password",
                new { userName = reviewerUserName, password = "Strong-Vendor#2026-Reviewer" },
                reviewerAntiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, passwordResponse.StatusCode);
                using var passwordJson = JsonDocument.Parse(await passwordResponse.Content.ReadAsStringAsync(cancellationToken));
                Assert.True(passwordJson.RootElement.GetProperty("requiresSecondFactor").GetBoolean());
            }

            using (var verifyResponse = await PostWithTokenAsync(reviewerClient, "/api/auth/totp/verify",
                new { code = GenerateTotpCode(reviewerAuthenticatorKey) },
                reviewerAntiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
            }
            (reviewerAntiForgeryToken, _) = await GetAntiforgeryTokenAsync(reviewerClient, cancellationToken);

            using (var licenseDecisionResponse = await PostWithTokenAsync(reviewerClient,
                $"/api/vendor/approvals/{licenseApprovalId:D}/decision",
                new { decision = "Approved", rowVersion = licenseApprovalRowVersion, comment = "must fail closed without signer" },
                reviewerAntiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.ServiceUnavailable, licenseDecisionResponse.StatusCode);
                using var json = JsonDocument.Parse(await licenseDecisionResponse.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("license_issuance_unavailable", json.RootElement.GetProperty("error").GetString());
            }

            foreach (var approval in new[]
            {
                (Id: customerApprovalId, RowVersion: customerApprovalRowVersion),
                (Id: installationApprovalId, RowVersion: installationApprovalRowVersion)
            })
            {
                using var response = await PostWithTokenAsync(reviewerClient,
                    $"/api/vendor/approvals/{approval.Id:D}/decision",
                    new { decision = "Approved", rowVersion = approval.RowVersion, comment = "Reviewed by a distinct administrator" },
                    reviewerAntiForgeryToken, cancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("Approved", json.RootElement.GetProperty("status").GetString());
                Assert.NotEqual(Guid.Empty, json.RootElement.GetProperty("reviewedByUserId").GetGuid());
            }

            await using (var licenseStateScope = factory.Services.CreateAsyncScope())
            {
                var db = licenseStateScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var license = await db.Licenses.AsNoTracking().SingleAsync(item => item.Id == licenseId, cancellationToken);
                Assert.Equal("Draft", license.Status);
                Assert.Equal("Pending", (await db.ApprovalRequests.AsNoTracking()
                    .SingleAsync(item => item.Id == licenseApprovalId, cancellationToken)).Status);
            }

            await using (var approvalVerificationScope = factory.Services.CreateAsyncScope())
            {
                var db = approvalVerificationScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var customer = await db.Customers.AsNoTracking().SingleAsync(item => item.Id == customerId, cancellationToken);
                var installation = await db.Installations.AsNoTracking().SingleAsync(item => item.Id == installationId, cancellationToken);
                Assert.Equal("Suspended", customer.Status);
                Assert.Equal("Suspended", installation.Status);
                Assert.Equal(2, await db.ApprovalRequests.CountAsync(item => item.Status == "Approved", cancellationToken));
                Assert.Equal(2, await db.AuditEvents.CountAsync(item =>
                    item.ActionCode == "vendor.approval.approved", cancellationToken));
                Assert.Equal(2, await db.AuditEvents.CountAsync(item =>
                    item.ActionCode == "vendor.customer.status-changed" || item.ActionCode == "vendor.installation.status-changed",
                    cancellationToken));
            }

            // A pending request becomes Stale if the resource RowVersion changes before review.
            byte[] staleCustomerVersion;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                staleCustomerVersion = (await db.Customers.AsNoTracking()
                    .SingleAsync(item => item.Id == customerId, cancellationToken)).RowVersion.ToArray();
            }

            Guid staleApprovalId;
            string staleApprovalRowVersion;
            using (var response = await PostWithTokenAsync(client, "/api/vendor/approvals/status-change",
                new
                {
                    resourceType = "customer",
                    resourceId = customerId,
                    targetStatus = "Active",
                    rowVersion = Convert.ToBase64String(staleCustomerVersion)
                }, antiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                staleApprovalId = json.RootElement.GetProperty("id").GetGuid();
                staleApprovalRowVersion = json.RootElement.GetProperty("rowVersion").GetString()!;
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                var customer = await db.Customers.SingleAsync(item => item.Id == customerId, cancellationToken);
                customer.DisplayName = "Changed after approval request";
                customer.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }

            using (var response = await PostWithTokenAsync(reviewerClient,
                $"/api/vendor/approvals/{staleApprovalId:D}/decision",
                new { decision = "Approved", rowVersion = staleApprovalRowVersion, comment = "stale resource check" },
                reviewerAntiForgeryToken, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                Assert.Equal("approval_resource_stale", json.RootElement.GetProperty("error").GetString());
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
                Assert.Equal("Stale", (await db.ApprovalRequests.AsNoTracking()
                    .SingleAsync(item => item.Id == staleApprovalId, cancellationToken)).Status);
                Assert.Equal("Suspended", (await db.Customers.AsNoTracking()
                    .SingleAsync(item => item.Id == customerId, cancellationToken)).Status);
            }

            await using (var raceVerificationScope = factory.Services.CreateAsyncScope())
            {
                var db = raceVerificationScope.ServiceProvider.GetRequiredService<VendorDbContext>();
                Assert.Equal(1, await db.Customers.CountAsync(item =>
                    item.CustomerNumber == racedCustomerNumber, cancellationToken));
                Assert.Equal(1, await db.AuditEvents.CountAsync(item =>
                    item.ActionCode == "vendor.customer.created" &&
                    item.SubjectId == createdCustomerId.ToString("D"), cancellationToken));
            }
        }
        finally
        {
            await DropDatabaseAsync(baseConnection, databaseName, CancellationToken.None);
        }
    }

    private sealed class CustomerInsertRaceGate : DbCommandInterceptor
    {
        private TaskCompletionSource _bothArrived = NewCompletionSource();
        private int _arrivals;
        private int _enabled;

        public void Enable()
        {
            Interlocked.Exchange(ref _arrivals, 0);
            Interlocked.Exchange(ref _preflightArrivals, 0);
            _bothArrived = NewCompletionSource();
            _bothPreflightChecks = NewCompletionSource();
            Volatile.Write(ref _enabled, 1);
        }

        private TaskCompletionSource _bothPreflightChecks = NewCompletionSource();
        private int _preflightArrivals;

        public int ObservedInsertCount => Volatile.Read(ref _arrivals);

        public void Disable() => Volatile.Write(ref _enabled, 0);

        public override async ValueTask<object?> ScalarExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result,
            CancellationToken cancellationToken = default)
        {
            await WaitForBothPreflightChecksAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            await WaitForBothPreflightChecksAsync(command, cancellationToken);
            return result;
        }

        private async Task WaitForBothPreflightChecksAsync(
            DbCommand command,
            CancellationToken cancellationToken)
        {
            // EF Core may execute AnyAsync as either a scalar command or a reader,
            // depending on query translation/provider. Gate the completed preflight
            // in both paths so both requests observe the number as available before
            // either INSERT reaches the unique index.
            if (Volatile.Read(ref _enabled) == 1 &&
                command.CommandText.Contains("VendorCustomers", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("CustomerNumber", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("EXISTS", StringComparison.OrdinalIgnoreCase))
            {
                if (Interlocked.Increment(ref _preflightArrivals) == 2)
                    _bothPreflightChecks.TrySetResult();

                await _bothPreflightChecks.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _enabled) == 1 &&
                command.CommandText.Contains("INSERT INTO [VendorCustomers]", StringComparison.OrdinalIgnoreCase))
            {
                if (Interlocked.Increment(ref _arrivals) == 2)
                    _bothArrived.TrySetResult();

                await _bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }

            return result;
        }

        private static TaskCompletionSource NewCompletionSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class AuditFailureSwitch
    {
        public bool FailNextAppend { get; set; }
    }

    private sealed class SwitchableVendorAuditWriter(
        VendorDbContext dbContext,
        TimeProvider timeProvider,
        AuditFailureSwitch failureSwitch) : IVendorAuditWriter
    {
        public Task<VendorAuditWriteResult> AppendAsync(
            VendorAuditWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (failureSwitch.FailNextAppend)
            {
                failureSwitch.FailNextAppend = false;
                throw new InvalidOperationException("Injected audit failure for rollback integration coverage.");
            }

            return new VendorAuditWriter(dbContext, timeProvider).AppendAsync(request, cancellationToken);
        }
    }

    private static async Task<string> GetTestUserNameAsync(WebApplicationFactory<Program> factory, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        return await db.Users.AsNoTracking().Where(user => user.UserName!.StartsWith("mutation-"))
            .Select(user => user.UserName!).SingleAsync(ct);
    }

    private static async Task<(string Token, string Cookie)> GetAntiforgeryTokenAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync("/api/auth/antiforgery", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var token = json.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return (token!, response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value => value.StartsWith("__Host-Faranou.Vendor.AntiForgery=", StringComparison.Ordinal)) ?? string.Empty
            : string.Empty);
    }

    private static async Task<HttpResponseMessage> PostWithTokenAsync(
        HttpClient client, string path, object body, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        return await client.SendAsync(request, ct);
    }

    private static async Task<HttpResponseMessage> SendPutAsync(
        HttpClient client, string path, object body, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        return await client.SendAsync(request, ct);
    }

    private static string GenerateTotpCode(string base32Secret)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var normalized = base32Secret.Trim().TrimEnd('=').ToUpperInvariant();
        var secret = new List<byte>(normalized.Length * 5 / 8);
        var buffer = 0;
        var bitsInBuffer = 0;
        foreach (var character in normalized)
        {
            var value = alphabet.IndexOf(character);
            if (value < 0) throw new InvalidOperationException("The persisted authenticator key is not valid Base32.");
            buffer = (buffer << 5) | value;
            bitsInBuffer += 5;
            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                secret.Add((byte)((buffer >> bitsInBuffer) & 0xff));
            }
        }

        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        using var hmac = new HMACSHA1(secret.ToArray());
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0f;
        var binaryCode = ((hash[offset] & 0x7f) << 24) | ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) | (hash[offset + 3] & 0xff);
        return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static async Task CreateDatabaseAsync(string configuredConnection, string databaseName, CancellationToken ct)
    {
        var master = new SqlConnectionStringBuilder(configuredConnection) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task DropDatabaseAsync(string configuredConnection, string databaseName, CancellationToken ct)
    {
        var master = new SqlConnectionStringBuilder(configuredConnection) { InitialCatalog = "master" }.ConnectionString;
        await using var connection = new SqlConnection(master);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(@databaseName) IS NOT NULL BEGIN ALTER DATABASE [" + databaseName +
            "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + databaseName + "]; END;";
        command.Parameters.AddWithValue("@databaseName", databaseName);
        await command.ExecuteNonQueryAsync(ct);
    }
}
