using System.Security.Claims;
using Infrastructure.Access;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// تست‌های Endpoint کاتالوگ دسترسی (Stage 2-D، Issue #444):
/// فقط مدیر نصب فعال (بازخوانی نقش از دیتابیس در هر درخواست) کاتالوگ را می‌بیند؛
/// کاتالوگ درونی‌سازگار است و کد entitlement جعلی نمی‌سازد (D-075/D-052).
/// </summary>
public sealed class AccessCatalogEndpointTests
{
    [Fact]
    public async Task Catalog_Reads_RequireAnActiveInstallationAdministratorFromTheDatabase()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        var ordinary = fixture.AddUser("accountant", "کاربر", "عادی", "Accountant");
        await fixture.Database.SaveChangesAsync();

        var anonymous = await Server.Api.AccessCatalogEndpoints.GetSubsystemsAsync(
            new DefaultHttpContext(), Anonymous(), fixture.Database, cancellationToken: default);
        Assert.Equal(StatusCodes.Status401Unauthorized, (anonymous as IStatusCodeHttpResult)?.StatusCode);

        var denied = await Server.Api.AccessCatalogEndpoints.GetSubsystemsAsync(
            new DefaultHttpContext(), Principal(ordinary.Id), fixture.Database, cancellationToken: default);
        Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);

        var allowed = await Server.Api.AccessCatalogEndpoints.GetSubsystemsAsync(
            new DefaultHttpContext(), Principal(admin.Id), fixture.Database, cancellationToken: default);
        var ok = Assert.IsType<Ok<AccessCatalogRegistry.AccessSubsystem[]>>(allowed);
        Assert.True(ok.Value!.Length > 0);
    }

    [Fact]
    public async Task Menus_And_Forms_RequireTheSameAdministratorGate()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        var ordinary = fixture.AddUser("accountant", "کاربر", "عادی", "Accountant");
        await fixture.Database.SaveChangesAsync();

        foreach (var denied in new[]
                 {
                     await Server.Api.AccessCatalogEndpoints.GetMenusAsync(new DefaultHttpContext(), Principal(ordinary.Id), fixture.Database, default),
                     await Server.Api.AccessCatalogEndpoints.GetFormsAsync(new DefaultHttpContext(), Principal(ordinary.Id), fixture.Database, default),
                 })
        {
            Assert.Equal(StatusCodes.Status403Forbidden, (denied as IStatusCodeHttpResult)?.StatusCode);
        }

        foreach (var allowed in new[]
                 {
                     await Server.Api.AccessCatalogEndpoints.GetMenusAsync(new DefaultHttpContext(), Principal(admin.Id), fixture.Database, default),
                     await Server.Api.AccessCatalogEndpoints.GetFormsAsync(new DefaultHttpContext(), Principal(admin.Id), fixture.Database, default),
                 })
        {
            Assert.Equal(StatusCodes.Status200OK, (allowed as IStatusCodeHttpResult)?.StatusCode);
        }
    }

    [Fact]
    public async Task Subsystems_ExposeTheDocumentedServerOwnedCatalog()
    {
        await using var fixture = new Fixture();
        var admin = fixture.AddUser("admin", "مدیر", "سیستم", Constants.Role.Admin);
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.AccessCatalogEndpoints.GetSubsystemsAsync(
            new DefaultHttpContext(), Principal(admin.Id), fixture.Database, default);
        var subsystems = Assert.IsType<Ok<AccessCatalogRegistry.AccessSubsystem[]>>(result).Value!;

        Assert.Equal(AccessCatalogRegistry.Subsystems.Count, subsystems.Length);
        Assert.Equal(subsystems.Length, subsystems.Select(x => x.Code).Distinct().Count());
        Assert.All(subsystems, x => Assert.False(string.IsNullOrWhiteSpace(x.Title)));

        var trade = Assert.Single(subsystems, x => x.Code == Faranou.Licensing.Contracts.LicenseSubsystemCodes.TradeImport);
        Assert.True(trade.EntitlementLinked);
        Assert.Equal(AccessCatalogRegistry.EntitlementScopeCompany, trade.EntitlementScope);
        Assert.Equal(Faranou.Licensing.Contracts.LicenseSubsystemCodes.TradeImport, trade.EntitlementCode);

        var finance = Assert.Single(subsystems, x => x.Code == Faranou.Licensing.Contracts.LicenseSubsystemCodes.Finance);
        Assert.Equal(AccessCatalogRegistry.EntitlementScopeInstallation, finance.EntitlementScope);

        var core = Assert.Single(subsystems, x => x.Code == "core");
        Assert.False(core.EntitlementLinked);
        Assert.Equal(AccessCatalogRegistry.EntitlementScopeNone, core.EntitlementScope);
        Assert.Null(core.EntitlementCode);
    }

    [Fact]
    public void Catalog_NeverInventsEntitlementCodes()
    {
        var allowed = new[]
        {
            Faranou.Licensing.Contracts.LicenseSubsystemCodes.Finance,
            Faranou.Licensing.Contracts.LicenseSubsystemCodes.Sales,
            Faranou.Licensing.Contracts.LicenseSubsystemCodes.Inventory,
            Faranou.Licensing.Contracts.LicenseSubsystemCodes.Treasury,
            Faranou.Licensing.Contracts.LicenseSubsystemCodes.TradeImport,
        };

        foreach (var subsystem in AccessCatalogRegistry.Subsystems.Where(x => x.EntitlementLinked))
        {
            Assert.True(Array.IndexOf(allowed, subsystem.EntitlementCode) >= 0,
                $"subsystem {subsystem.Code} links an unknown entitlement code {subsystem.EntitlementCode}");
            Assert.Equal(subsystem.EntitlementCode, subsystem.Code);
        }

        foreach (var subsystem in AccessCatalogRegistry.Subsystems.Where(x => !x.EntitlementLinked))
        {
            Assert.Equal(AccessCatalogRegistry.EntitlementScopeNone, subsystem.EntitlementScope);
            Assert.Null(subsystem.EntitlementCode);
        }
    }

    [Fact]
    public void MenuAndFormCatalog_AreInternallyConsistent()
    {
        var subsystemCodes = AccessCatalogRegistry.Subsystems.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);

        foreach (var menu in AccessCatalogRegistry.Menus)
        {
            Assert.True(subsystemCodes.Contains(menu.SubsystemCode), $"menu «{menu.Label}» references unknown subsystem {menu.SubsystemCode}");
            Assert.False(string.IsNullOrWhiteSpace(menu.Label));
            if (menu.Status == AccessCatalogRegistry.MenuStatusActive)
            {
                Assert.False(string.IsNullOrWhiteSpace(menu.Route), $"active menu «{menu.Label}» must carry a real route");
            }
            else
            {
                Assert.Equal(AccessCatalogRegistry.MenuStatusComingSoon, menu.Status);
                Assert.Null(menu.Route);
            }
        }

        var routes = AccessCatalogRegistry.Menus.Where(x => x.Route is not null).Select(x => x.Route!).ToArray();
        Assert.Equal(routes.Length, routes.Distinct().Count());

        foreach (var form in AccessCatalogRegistry.Forms)
        {
            Assert.True(subsystemCodes.Contains(form.SubsystemCode), $"form {form.Route} references unknown subsystem {form.SubsystemCode}");
            Assert.False(string.IsNullOrWhiteSpace(form.Title));
            Assert.False(string.IsNullOrWhiteSpace(form.Parent));
        }

        var formRoutes = AccessCatalogRegistry.Forms.Select(x => x.Route).ToArray();
        Assert.Equal(formRoutes.Length, formRoutes.Distinct().Count());
    }

    private static ClaimsPrincipal Principal(Guid userId) => new(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("AccountKind", "User"),
        ], "test"));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        }

        public Data.DatabaseContext Database { get; }
        public Guid AdminId { get; private set; }
        public Domain.User AddUser(string username, string firstName, string lastName, string roleName)
        {
            var role = Database.Roles.Local.FirstOrDefault(x => x.Name == roleName)
                ?? new Domain.Role(roleName) { IsActive = true };
            if (role.Id == Guid.Empty || !Database.Roles.Local.Contains(role))
                Database.Roles.Add(role);

            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                IsActive = true,
                Role = role,
                Password = "not-a-real-hash",
            };
            Database.Users.Add(user);
            if (roleName == Constants.Role.Admin)
                AdminId = user.Id;
            return user;
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
