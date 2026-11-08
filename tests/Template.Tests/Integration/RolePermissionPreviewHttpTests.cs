using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// The role permission preview over the real route table (Issue #444, Stage 2, PR-D). The
/// candidate codes arrive as repeated query parameters, so the endpoint must bind them from the
/// query — a bare complex parameter would be inferred as a body, which a GET must not have
/// ("Body was inferred but the method does not allow inferred body parameters" — the startup
/// failure this test class guards against: the route table cannot even build). The handler-level
/// unit tests call the method directly and cannot catch that, so this HTTP round-trip is the
/// regression net. Read-only end to end: 200 + the server-computed diff, no-store, and no write.
/// </summary>
public sealed class RolePermissionPreviewHttpTests : IClassFixture<FaranouApplicationFactory>
{
        private readonly FaranouApplicationFactory factory;

        public RolePermissionPreviewHttpTests(FaranouApplicationFactory factory)
        {
                this.factory = factory;
        }

        private async Task<(SeededUser Admin, Guid CompanyId, Guid RoleId)> SeedAsync()
        {
                var password = $"Pw-{Guid.NewGuid():N}";
                var (admin, companyId, roleId) = await factory.WithDatabaseAsync(async database =>
                {
                        var hasher = new Infrastructure.Security.PasswordHashService();
                        var adminRole = await AdminRoleSeed.GetOrCreateAsync(database);
                        // TestData.Unique keeps the username inside the 3–20 character login contract.
                        var admin = new Domain.User(emailAddress: $"{TestData.Unique("rpp")}@example.test")
                        {
                                Username = TestData.Unique("rpp"),
                                Password = hasher.Hash(password: password),
                                IsActive = true,
                                Role = adminRole,
                        };

                        var company = new Domain.Company { Name = $"شرکت-{Guid.NewGuid():N}", IsActive = true };
                        var role = new Domain.CompanyRole { Company = company, TenantId = company.Id, Code = "VIEWER", Name = "نمایش‌دهنده", IsActive = true };
                        database.Users.Add(admin);
                        database.Companies.Add(company);
                        database.CompanyRoles.Add(role);

                        // The candidate codes must be registered in the catalog; the role currently holds one of
                        // them. The fixture database is shared by the class, so the definitions are reused
                        // when they already exist — a second insert would break the unique code index (SQL
                        // Server) and make the registered-code count check answer 400 (in-memory).
                        var current = await database.PermissionDefinitions
                                .SingleOrDefaultAsync(definition => definition.Code == "users.list", cancellationToken: default)
                            ?? database.PermissionDefinitions.Add(new Domain.PermissionDefinition
                            {
                                    Code = "users.list",
                                    Name = "users.list",
                                    Description = "test permission",
                            }).Entity;
                        _ = await database.PermissionDefinitions
                                .SingleOrDefaultAsync(definition => definition.Code == "trade.form.party.delete", cancellationToken: default)
                            ?? database.PermissionDefinitions.Add(new Domain.PermissionDefinition
                            {
                                    Code = "trade.form.party.delete",
                                    Name = "trade.form.party.delete",
                                    Description = "test permission",
                            }).Entity;
                        database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
                        {
                                Role = role,
                                RoleId = role.Id,
                                Permission = current,
                        });
                        await database.SaveChangesAsync();
                        return (new SeededUser(admin.Id, admin.Username!, password), company.Id, role.Id);
                });

                return (admin, companyId, roleId);
        }

        [Fact]
        public async Task RepeatedQueryParameters_AreBoundAndTheDiffIsComputedServerSide()
        {
                var (admin, companyId, roleId) = await SeedAsync();
                var client = factory.CreateBrowser();
                await Browser.SignInAsync(client, admin);

                var response = await client.GetAsync(
                        $"/api/v1/admin/companies/{companyId}/roles/{roleId}/permissions/preview" +
                        "?permissionCodes=users.list&permissionCodes=trade.form.party.delete");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(response.Headers.CacheControl?.NoStore == true, "Cache-Control: no-store is expected on the preview.");
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(roleId.ToString(), body.GetProperty("roleId").GetString());
                Assert.Equal(0, body.GetProperty("activeMemberCount").GetInt32());
                Assert.Equal(["users.list"], body.GetProperty("currentPermissionCodes").EnumerateArray().Select(code => code.GetString()).ToArray());
                Assert.Equal(["trade.form.party.delete"], body.GetProperty("addedPermissionCodes").EnumerateArray().Select(code => code.GetString()).ToArray());
                Assert.Empty(body.GetProperty("removedPermissionCodes").EnumerateArray());
                Assert.Equal(["trade.form.party.delete"], body.GetProperty("sensitiveChangedPermissionCodes").EnumerateArray().Select(code => code.GetString()).ToArray());

                // The preview is strictly read-only: no new audit event beyond the sign-in itself and
                // no permission change.
                await factory.WithDatabaseAsync(async database =>
                {
                        var events = await database.SecurityAuditEvents.ToListAsync();
                        Assert.All(events, auditEvent => Assert.NotEqual("CompanyRole", auditEvent.EntityType));
                        var role = await database.CompanyRoles
                                .Include(item => item.RolePermissions)
                                .ThenInclude(rolePermission => rolePermission.Permission)
                                .SingleAsync(item => item.Id == roleId);
                        Assert.Equal(["users.list"], role.RolePermissions
                                .Select(rolePermission => rolePermission.Permission.Code).ToArray());
                        return 0;
                });
        }

        [Fact]
        public async Task ARepeatedUnknownCode_IsStillRejectedByTheSaveContract()
        {
                var (admin, companyId, roleId) = await SeedAsync();
                var client = factory.CreateBrowser();
                await Browser.SignInAsync(client, admin);

                var response = await client.GetAsync(
                        $"/api/v1/admin/companies/{companyId}/roles/{roleId}/permissions/preview" +
                        "?permissionCodes=users.list&permissionCodes=users.list");

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("error").GetString()));
        }
}
