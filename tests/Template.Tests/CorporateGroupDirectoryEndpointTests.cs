using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class CorporateGroupDirectoryEndpointTests
{
    [Fact]
    public async Task Directory_ReturnsOnlyUsersFromCurrentMemberCompaniesAndAllowlistedFields()
    {
        await using var database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var scenario = await CorporateGroupScenario.CreateAsync(database);
        var groupId = await scenario.CreateHoldingGroupAsync();

        var result = await Server.Api.CorporateGroupDirectoryEndpoints.GetDirectoryAsync(
            groupId,
            new DefaultHttpContext(),
            TenantFixture.CompanyUser(scenario.Viewer),
            database,
            new Infrastructure.Tenancy.CompanyAccessService(database),
            TimeProvider.System,
            page: 1,
            pageSize: 100,
            cancellationToken: default);

        var ok = Assert.IsType<Ok<Server.Api.CorporateGroupDirectoryResponse>>(result);
        Assert.Equal(8, ok.Value!.TotalCount);
        Assert.DoesNotContain(ok.Value.Items, item => item.CompanyId == scenario.Other);
        Assert.Contains(ok.Value.Items, item => item.CompanyId == scenario.Holding);
        Assert.Contains(ok.Value.Items, item => item.CompanyId == scenario.SubA);
        Assert.Contains(ok.Value.Items, item => item.CompanyId == scenario.SubB);

        var properties = typeof(Server.Api.CorporateGroupDirectoryItemResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.Equal(
            new[] { "UserId", "DisplayName", "Username", "CompanyId", "CompanyName", "IsMembershipActive" }
                .OrderBy(name => name, StringComparer.Ordinal),
            properties.OrderBy(name => name, StringComparer.Ordinal));
        Assert.DoesNotContain(properties, name =>
            name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Phone", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Role", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Permission", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Login", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Ip", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Directory_HidesUnknownAndUnauthorizedGroupsTheSameWay()
    {
        await using var database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var scenario = await CorporateGroupScenario.CreateAsync(database);
        var groupId = await scenario.CreateHoldingGroupAsync();
        var access = new Infrastructure.Tenancy.CompanyAccessService(database);

        var unauthorized = await Server.Api.CorporateGroupDirectoryEndpoints.GetDirectoryAsync(
            groupId,
            new DefaultHttpContext(),
            TenantFixture.CompanyUser(scenario.Outsider),
            database,
            access,
            TimeProvider.System,
            cancellationToken: default);
        var unknown = await Server.Api.CorporateGroupDirectoryEndpoints.GetDirectoryAsync(
            Guid.NewGuid(),
            new DefaultHttpContext(),
            TenantFixture.CompanyUser(scenario.Viewer),
            database,
            access,
            TimeProvider.System,
            cancellationToken: default);

        Assert.IsType<NotFound>(unauthorized);
        Assert.IsType<NotFound>(unknown);
    }

    [Fact]
    public async Task Directory_FailsClosedWhenAnyGroupMemberCrossesCustomerBoundary()
    {
        await using var database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var scenario = await CorporateGroupScenario.CreateAsync(database);
        var groupId = await scenario.CreateHoldingGroupAsync();
        var foreignCompany = new Domain.Company
        {
            Name = "شرکت خارج از مرز",
            CustomerBoundaryId = Guid.NewGuid(),
            IsActive = true,
        };
        database.Companies.Add(foreignCompany);
        database.CorporateGroupMembers.Add(new Domain.CorporateGroupMember
        {
            CorporateGroupId = groupId,
            CompanyId = foreignCompany.Id,
            ValidFrom = CorporateGroupScenario.YearStart,
        });
        await database.SaveChangesAsync();

        var result = await Server.Api.CorporateGroupDirectoryEndpoints.GetDirectoryAsync(
            groupId,
            new DefaultHttpContext(),
            TenantFixture.CompanyUser(scenario.Viewer),
            database,
            new Infrastructure.Tenancy.CompanyAccessService(database),
            TimeProvider.System,
            cancellationToken: default);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Directory_SearchesOnlyTheAllowlistedIdentityFields_AndClampsPagination()
    {
        await using var database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var scenario = await CorporateGroupScenario.CreateAsync(database);
        var groupId = await scenario.CreateHoldingGroupAsync();
        var user = new Domain.User("directory@example.test")
        {
            Username = "directory.user",
            FirstName = "نیما",
            LastName = "آزمایشی",
            IsActive = true,
        };
        database.Users.Add(user);
        database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
        {
            UserId = user.Id,
            TenantId = scenario.SubA,
            RoleId = scenario.AdminRole[scenario.SubA],
            IsActive = false,
        });
        await database.SaveChangesAsync();

        var result = await Server.Api.CorporateGroupDirectoryEndpoints.GetDirectoryAsync(
            groupId,
            new DefaultHttpContext(),
            TenantFixture.CompanyUser(scenario.Viewer),
            database,
            new Infrastructure.Tenancy.CompanyAccessService(database),
            TimeProvider.System,
            page: 0,
            pageSize: 1000,
            search: "نیما",
            cancellationToken: default);

        var ok = Assert.IsType<Ok<Server.Api.CorporateGroupDirectoryResponse>>(result);
        Assert.Equal(1, ok.Value!.TotalCount);
        var item = Assert.Single(ok.Value.Items);
        Assert.Equal(user.Id, item.UserId);
        Assert.Equal("نیما آزمایشی", item.DisplayName);
        Assert.Equal("directory.user", item.Username);
        Assert.False(item.IsMembershipActive);
        Assert.Equal(1, ok.Value.Page);
        Assert.Equal(100, ok.Value.PageSize);
    }
}
