using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Behavioural rate-limit tests for the sensitive administrative identity reads
/// (`ApiSensitivePolicy` on the user list, user details, company user directory and role
/// member routes — Issue #522 / PR #538). The route-metadata side is covered by
/// <c>ApiSensitiveReadRateLimitTests</c>; this class proves the real pipeline behaviour:
/// 429 with <c>Retry-After</c> and a generic body, no bypass via query, route or endpoint
/// hopping (one shared per-user partition), other users unaffected, non-sensitive API
/// unaffected, and anonymous callers throttled on their address partition.
/// </summary>
public sealed class SensitiveReadRateLimitBehaviorTests : IClassFixture<SensitiveReadRateLimitBehaviorTests.Factory>
{
        public sealed class Factory : FaranouApplicationFactory
        {
                protected override IDictionary<string, string?> Settings
                {
                        get
                        {
                                var settings = base.Settings;
                                settings["RateLimiting:ApiSensitive:PermitLimit"] = "3";
                                settings["RateLimiting:ApiSensitive:WindowSeconds"] = "3600";
                                return settings;
                        }
                }
        }

        private readonly Factory factory;

        public SensitiveReadRateLimitBehaviorTests(Factory factory)
        {
                this.factory = factory;
        }

        private async Task<SeededUser> AddInstallationAdministratorAsync(string prefix)
        {
                var password = $"Pw-{Guid.NewGuid():N}";

                return await factory.WithDatabaseAsync(async database =>
                {
                        var hasher = new Infrastructure.Security.PasswordHashService();
                        var role = new Domain.Role(name: Constants.Role.Admin) { IsActive = true };
                        var user = new Domain.User(emailAddress: $"{TestData.Unique(prefix)}@example.test")
                        {
                                Username = TestData.Unique(prefix),
                                Password = hasher.Hash(password: password),
                                IsActive = true,
                                Role = role,
                        };

                        database.Users.Add(user);
                        await database.SaveChangesAsync();

                        return new SeededUser(user.Id, user.Username!, password);
                });
        }

        [Fact]
        public async Task TheSensitiveReadLimit_RejectsWith429AndRetryAfter_AndCannotBeBypassed()
        {
                var admin = await AddInstallationAdministratorAsync(prefix: "srl");
                var otherAdmin = await AddInstallationAdministratorAsync(prefix: "sro");

                var firstClient = factory.CreateBrowser();
                await Browser.SignInAsync(firstClient, admin);

                // (1) Positive: up to the configured permit limit the reads succeed.
                Assert.Equal(HttpStatusCode.OK, (await firstClient.GetAsync("/api/v1/admin/users?page=1&pageSize=10&descending=false")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await firstClient.GetAsync("/api/v1/admin/users?page=2&pageSize=10&descending=false")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await firstClient.GetAsync("/api/v1/admin/users?page=1&pageSize=10&search=abc&descending=false")).StatusCode);

                // (2) Negative: the next read is rejected with 429, Retry-After, no-store and a
                // generic problem body that leaks neither the account nor its e-mail address.
                var rejected = await firstClient.GetAsync("/api/v1/admin/users");
                Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
                Assert.True(rejected.Headers.Contains("Retry-After"), "Retry-After is expected on 429.");
                Assert.True(rejected.Headers.CacheControl?.NoStore == true, "Cache-Control: no-store is expected on 429.");
                Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
                var body = await rejected.Content.ReadAsStringAsync();
                Assert.Contains("بیش از حد مجاز", body);
                Assert.DoesNotContain(admin.Username, body);
                Assert.DoesNotContain("@example.test", body);

                // (3) The same user cannot bypass the exhausted partition by changing the query.
                Assert.Equal(
                        HttpStatusCode.TooManyRequests,
                        (await firstClient.GetAsync("/api/v1/admin/users?page=9&pageSize=5&sort=name&descending=true&search=xyz")).StatusCode);

                // (4) …nor by a different sensitive route: all sensitive reads of one user share
                // the per-user partition, so endpoint hopping cannot multiply the budget.
                Assert.Equal(
                        HttpStatusCode.TooManyRequests,
                        (await firstClient.GetAsync($"/api/v1/admin/users/{Guid.NewGuid()}")).StatusCode);
                Assert.Equal(
                        HttpStatusCode.TooManyRequests,
                        (await firstClient.GetAsync($"/api/v1/admin/companies/{Guid.NewGuid()}/roles/{Guid.NewGuid()}/members")).StatusCode);

                // (5) A different signed-in administrator has an independent partition and still works.
                var secondClient = factory.CreateBrowser();
                await Browser.SignInAsync(secondClient, otherAdmin);
                Assert.Equal(HttpStatusCode.OK, (await secondClient.GetAsync("/api/v1/admin/users?page=1&pageSize=10&descending=false")).StatusCode);

                // (6) Ordinary (non-sensitive) API reads of the throttled user are unaffected.
                Assert.Equal(HttpStatusCode.OK, (await firstClient.GetAsync("/api/v1/me/membership-requests")).StatusCode);
        }

        [Fact]
        public async Task AnonymousCallsToSensitiveReads_AreUnauthorized_AndThenThrottledPerAddress()
        {
                var anonymous = factory.CreateBrowser();

                // The endpoint requires authentication; unauthenticated callers get 401 — and the
                // attempts still consume the address partition of the sensitive policy.
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/users")).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/users")).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/users")).StatusCode);
                Assert.Equal(HttpStatusCode.TooManyRequests, (await anonymous.GetAsync("/api/v1/admin/users")).StatusCode);
                Assert.True(
                        (await anonymous.GetAsync("/api/v1/admin/users")).Headers.Contains("Retry-After"),
                        "Retry-After is expected on the throttled anonymous partition as well.");
        }
}

/// <summary>
/// HTTP-level authorization of the administrative access-management endpoints through the
/// real pipeline (Issue #444 mission review): anonymous callers receive 401, authenticated
/// non-administrators receive an explicit 403 (the UI is never the only control) and an
/// installation administrator may read.
/// </summary>
public sealed class AdministrativeEndpointAuthorizationHttpTests : IClassFixture<FaranouApplicationFactory>
{
        private readonly FaranouApplicationFactory factory;

        public AdministrativeEndpointAuthorizationHttpTests(FaranouApplicationFactory factory)
        {
                this.factory = factory;
        }

        private async Task<SeededCompany> AddCompanyAsync()
        {
                return await factory.WithDatabaseAsync(async database =>
                {
                        var company = new Domain.Company { Name = TestData.Unique("شرکت "), IsActive = true };
                        var role = new Domain.CompanyRole
                        {
                                TenantId = company.Id,
                                Company = company,
                                Code = "accountant",
                                Name = "حسابدار",
                                IsActive = true,
                        };

                        database.Companies.Add(company);
                        database.CompanyRoles.Add(role);
                        await database.SaveChangesAsync();

                        return new SeededCompany(company.Id, company.Name, role.Id);
                });
        }

        private async Task<(SeededUser Admin, SeededUser Member)> AddUsersAsync(SeededCompany company)
        {
                var adminPassword = $"Pw-{Guid.NewGuid():N}";
                var memberPassword = $"Pw-{Guid.NewGuid():N}";

                return await factory.WithDatabaseAsync(async database =>
                {
                        var hasher = new Infrastructure.Security.PasswordHashService();
                        var adminRole = new Domain.Role(name: Constants.Role.Admin) { IsActive = true };
                        var admin = new Domain.User(emailAddress: $"{TestData.Unique("aez")}@example.test")
                        {
                                Username = TestData.Unique("aez"),
                                Password = hasher.Hash(password: adminPassword),
                                IsActive = true,
                                Role = adminRole,
                        };
                        var member = new Domain.User(emailAddress: $"{TestData.Unique("mez")}@example.test")
                        {
                                Username = TestData.Unique("mez"),
                                Password = hasher.Hash(password: memberPassword),
                                IsActive = true,
                        };

                        database.Users.AddRange(admin, member);
                        database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
                        {
                                UserId = member.Id,
                                TenantId = company.Id,
                                RoleId = company.RoleId,
                                IsActive = true,
                        });
                        await database.SaveChangesAsync();

                        return (new SeededUser(admin.Id, admin.Username!, adminPassword),
                                new SeededUser(member.Id, member.Username!, memberPassword));
                });
        }

        [Fact]
        public async Task AdministrativeReads_Are401ForAnonymous_403ForMembers_And200ForInstallationAdministrators()
        {
                var company = await AddCompanyAsync();
                var (admin, member) = await AddUsersAsync(company);

                // Anonymous: every administrative read is 401 through the real pipeline.
                var anonymous = factory.CreateBrowser();
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/users")).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/permission-catalog")).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/admin/companies/{company.Id}/roles")).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/companies/{company.Id}/users")).StatusCode);

                // Authenticated regular member: an explicit 403 payload, never data.
                var memberClient = factory.CreateBrowser();
                await Browser.SignInAsync(memberClient, member);
                var deniedPaths = new[]
                {
                        "/api/v1/admin/users?page=1&pageSize=10&descending=false",
                        "/api/v1/admin/permission-catalog",
                        $"/api/v1/admin/companies/{company.Id}/roles",
                        $"/api/v1/admin/companies/{company.Id}/roles/{company.RoleId}/members",
                        $"/api/v1/companies/{company.Id}/users",
                };

                foreach (var path in deniedPaths)
                {
                        var denied = await memberClient.GetAsync(path);
                        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
                        var deniedBody = await denied.Content.ReadAsStringAsync();
                        Assert.DoesNotContain("@example.test", deniedBody);
                }

                // The member's own directory is not disclosed either: the response cannot contain
                // the administrator account that the member has no permission to list.
                var memberDirectory = await memberClient.GetAsync($"/api/v1/companies/{company.Id}/users");
                Assert.Equal(HttpStatusCode.Forbidden, memberDirectory.StatusCode);

                // Installation administrator: the same reads succeed.
                var adminClient = factory.CreateBrowser();
                await Browser.SignInAsync(adminClient, admin);
                Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/api/v1/admin/users?page=1&pageSize=10&descending=false")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/api/v1/admin/permission-catalog")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync($"/api/v1/admin/companies/{company.Id}/roles")).StatusCode);
        }
}
