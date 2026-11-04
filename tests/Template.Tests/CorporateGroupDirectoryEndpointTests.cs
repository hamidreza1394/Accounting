using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The corporate-group directory (D-065 decision 2, D-071): read-only, allow-listed user
/// names of the member companies of one's own group, gated by an explicit permission and
/// fail-closed on the customer boundary. It never exposes roles, permissions, usernames,
/// contact data or security data of the other companies.
/// </summary>
public sealed class CorporateGroupDirectoryEndpointTests
{
    [Fact]
    public async Task Directory_MemberWithPermission_SeesOnlyTheUsersOfTheSameGroup()
    {
        await using var fixture = new Fixture();
        var (group, companyA, _) = fixture.AddGroupWithCompanies("گروه نمونه");
        var manager = fixture.AddMemberWithPermission(companyA, Server.Api.CorporateGroupDirectoryEndpoints.ViewGroupDirectoryPermission);
        var inGroupA = fixture.AddMember(fixture.AddRole(companyA, "CLERK"), "ali");
        var companyB = fixture.AddCompany("شرکت ب", boundary: fixture.BoundaryId);
        fixture.AddGroupMember(group, companyB);
        var inGroupB = fixture.AddMember(fixture.AddRole(companyB, "CLERK"), "reza");
        fixture.AddMember(fixture.AddRole(companyB, "CLERK"), "inactive-member", membershipActive: false);
        var outsideCompany = fixture.AddCompany("شرکت بیرون گروه", boundary: fixture.BoundaryId);
        var outsider = fixture.AddMember(fixture.AddRole(outsideCompany, "CLERK"), "outside-user");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetDirectory(manager.Id, group.Id);
        var ok = Assert.IsType<Ok<Server.Api.CorporateGroupDirectoryResponse>>(result);
        Assert.Equal(3, ok.Value!.TotalCount); // manager (company A) + one member per company
        Assert.All(ok.Value.Items, item =>
        {
            Assert.Contains(item.UserId, new[] { manager.Id, inGroupA.Id, inGroupB.Id });
            Assert.DoesNotContain(item.UserId, new[] { outsider.Id });
        });

        // The allow-listed item contract carries no username, role or security field.
        var fields = typeof(Server.Api.CorporateGroupDirectoryItem).GetProperties().Select(p => p.Name).ToArray();
        Assert.Equal(["CompanyId", "CompanyName", "FirstName", "LastName", "UserId"], fields.Order().ToArray());
    }

    [Fact]
    public async Task Directory_WithoutTheExplicitPermission_IsRefusedForTheGroupMember()
    {
        await using var fixture = new Fixture();
        var (group, companyA, _) = fixture.AddGroupWithCompanies("گروه نمونه");
        var member = fixture.AddMember(fixture.AddRole(companyA, "CLERK"), "plain-member");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetDirectory(member.Id, group.Id);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Directory_NonMemberOfTheGroup_GetsWithoutDisclosure()
    {
        await using var fixture = new Fixture();
        var (group, _, _) = fixture.AddGroupWithCompanies("گروه نمونه");
        var outsideCompany = fixture.AddCompany("شرکت بیرون", boundary: fixture.BoundaryId);
        var outsider = fixture.AddMemberWithPermission(outsideCompany, Server.Api.CorporateGroupDirectoryEndpoints.ViewGroupDirectoryPermission);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetDirectory(outsider.Id, group.Id);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Directory_WithMixedCustomerBoundaries_FailsClosed()
    {
        await using var fixture = new Fixture();
        var owner = fixture.AddCompany("شرکت مادر", fixture.BoundaryId);
        var otherBoundaryCompany = fixture.AddCompany("شرکت مرز دیگر", boundary: Guid.NewGuid());
        var group = fixture.AddGroup("گروه ناهمگون", owner);
        fixture.AddGroupMember(group, owner);
        fixture.AddGroupMember(group, otherBoundaryCompany);
        var manager = fixture.AddMemberWithPermission(owner, Server.Api.CorporateGroupDirectoryEndpoints.ViewGroupDirectoryPermission);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetDirectory(manager.Id, group.Id);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Directory_WithoutAProvisionedBoundary_FailsClosed()
    {
        await using var fixture = new Fixture();
        var companyA = fixture.AddCompany("شرکت الف", boundary: null);
        var companyB = fixture.AddCompany("شرکت ب", boundary: null);
        var group = fixture.AddGroup("گروه بی‌مرز", companyA);
        fixture.AddGroupMember(group, companyA);
        fixture.AddGroupMember(group, companyB);
        var manager = fixture.AddMemberWithPermission(companyA, Server.Api.CorporateGroupDirectoryEndpoints.ViewGroupDirectoryPermission);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetDirectory(manager.Id, group.Id);

        Assert.Equal(StatusCodes.Status404NotFound, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Directory_AnEndedGroupMembership_LeavesTheScope()
    {
        await using var fixture = new Fixture();
        var (group, companyA, companyB) = fixture.AddGroupWithCompanies("گروه نمونه");
        var leftCompanyUser = fixture.AddMember(fixture.AddRole(companyB, "CLERK"), "left-user");
        fixture.EndGroupMembership(group, companyB);
        var manager = fixture.AddMemberWithPermission(companyA, Server.Api.CorporateGroupDirectoryEndpoints.ViewGroupDirectoryPermission);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetDirectory(manager.Id, group.Id);
        var ok = Assert.IsType<Ok<Server.Api.CorporateGroupDirectoryResponse>>(result);
        Assert.Single(ok.Value!.Items);
        Assert.Equal(companyA.Id, ok.Value.Items.Single().CompanyId);
        Assert.DoesNotContain(ok.Value.Items, item => item.UserId == leftCompanyUser.Id);
    }

    [Fact]
    public async Task Directory_SearchFiltersByAllowlistedNameFieldsOnly()
    {
        await using var fixture = new Fixture();
        var (group, companyA, companyB) = fixture.AddGroupWithCompanies("گروه نمونه");
        var manager = fixture.AddMemberWithPermission(companyA, Server.Api.CorporateGroupDirectoryEndpoints.ViewGroupDirectoryPermission);
        fixture.AddNamedMember(companyA, "علی", "محمدی");
        fixture.AddNamedMember(companyB, "علی‌رضا", "احمدی");
        fixture.AddNamedMember(companyB, "مریم", "احمدی");
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.GetDirectory(manager.Id, group.Id, search: "احمدی");
        var ok = Assert.IsType<Ok<Server.Api.CorporateGroupDirectoryResponse>>(result);
        Assert.Equal(2, ok.Value!.TotalCount);
        Assert.All(ok.Value.Items, item => Assert.NotEqual(companyA.Id, item.CompanyId));
    }

    [Fact]
    public async Task Directory_UnknownOrInactiveGroup_Returns404()
    {
        await using var fixture = new Fixture();
        var (group, companyA, _) = fixture.AddGroupWithCompanies("گروه نمونه");
        var manager = fixture.AddMemberWithPermission(companyA, Server.Api.CorporateGroupDirectoryEndpoints.ViewGroupDirectoryPermission);
        group.IsActive = false;
        await fixture.Database.SaveChangesAsync();

        var missing = await fixture.GetDirectory(manager.Id, Guid.NewGuid());
        Assert.Equal(StatusCodes.Status404NotFound, (missing as IStatusCodeHttpResult)?.StatusCode);

        var inactive = await fixture.GetDirectory(manager.Id, group.Id);
        Assert.Equal(StatusCodes.Status404NotFound, (inactive as IStatusCodeHttpResult)?.StatusCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = NewContext();
            BoundaryId = Guid.NewGuid();
        }

        private readonly string _databaseName = Guid.NewGuid().ToString();

        public Data.DatabaseContext NewContext() => new(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(_databaseName).Options);

        public Data.DatabaseContext Database { get; }
        public Guid BoundaryId { get; }

        public Infrastructure.Tenancy.CompanyAccessService CompanyAccess => new(Database);

        public Domain.Company AddCompany(string name, Guid? boundary)
        {
            var company = new Domain.Company { Name = name, IsActive = true, CustomerBoundaryId = boundary };
            Database.Companies.Add(company);
            return company;
        }

        public Domain.CorporateGroup AddGroup(string name, Domain.Company owner)
        {
            var group = new Domain.CorporateGroup
            {
                Code = $"G-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                Name = name,
                OwnerCompanyId = owner.Id,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = owner.Id,
            };
            Database.CorporateGroups.Add(group);
            return group;
        }

        /// <summary>Owner company plus one member company, all inside one shared boundary.</summary>
        public (Domain.CorporateGroup Group, Domain.Company Owner, Domain.Company Member)
            AddGroupWithCompanies(string name)
        {
            var owner = AddCompany("شرکت مادر", BoundaryId);
            var member = AddCompany("شرکت زیرمجموعه", BoundaryId);
            var group = AddGroup(name, owner);
            AddGroupMember(group, owner);
            AddGroupMember(group, member);
            return (group, owner, member);
        }

        public void AddGroupMember(Domain.CorporateGroup group, Domain.Company company)
        {
            Database.CorporateGroupMembers.Add(new Domain.CorporateGroupMember
            {
                CorporateGroupId = group.Id,
                CompanyId = company.Id,
                MemberRole = Domain.CorporateGroupMemberRole.Subsidiary,
                ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = group.CreatedByUserId,
            });
        }

        public void EndGroupMembership(Domain.CorporateGroup group, Domain.Company company)
        {
            var member = Database.CorporateGroupMembers.Local
                .Single(item => item.CorporateGroupId == group.Id && item.CompanyId == company.Id);
            member.ValidTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        }

        public Domain.CompanyRole AddRole(Domain.Company company, string code)
        {
            var role = new Domain.CompanyRole
            {
                TenantId = company.Id,
                Company = company,
                Code = code,
                Name = code,
                IsActive = true,
            };
            Database.CompanyRoles.Add(role);
            return role;
        }

        public Domain.User AddMember(Domain.CompanyRole role, string username, bool membershipActive = true)
        {
            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                FirstName = username,
                IsActive = true,
                Password = "x",
            };
            Database.Users.Add(user);
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = user.Id,
                TenantId = role.TenantId,
                Role = role,
                RoleId = role.Id,
                IsActive = membershipActive,
                CreatedAtUtc = DateTime.UtcNow,
            });
            return user;
        }

        public Domain.User AddNamedMember(Domain.Company company, string firstName, string lastName)
        {
            var role = AddRole(company, $"R-{Guid.NewGuid():N}"[..10].ToUpperInvariant());
            var user = new Domain.User($"{firstName}-{lastName}@example.test")
            {
                Username = $"{firstName}{lastName}",
                FirstName = firstName,
                LastName = lastName,
                IsActive = true,
                Password = "x",
            };
            Database.Users.Add(user);
            Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
            {
                UserId = user.Id,
                TenantId = company.Id,
                Role = role,
                RoleId = role.Id,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
            });
            return user;
        }

        private readonly Dictionary<string, Guid> _permissionIds = new();

        public Domain.User AddMemberWithPermission(Domain.Company company, string permissionCode)
        {
            var permission = new Domain.PermissionDefinition
            {
                Code = permissionCode,
                Name = permissionCode,
                Description = "test permission",
            };
            Database.PermissionDefinitions.Add(permission);
            _permissionIds[permissionCode] = permission.Id;
            var role = new Domain.CompanyRole
            {
                TenantId = company.Id,
                Company = company,
                Code = "MANAGER",
                Name = "مدیر",
                IsActive = true,
            };
            Database.CompanyRoles.Add(role);
            Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission
            {
                Role = role,
                PermissionId = _permissionIds[permissionCode],
            });
            return AddMember(role, "manager");
        }

        public Task<IResult> GetDirectory(Guid userId, Guid groupId, string? search = null) =>
            Server.Api.CorporateGroupDirectoryEndpoints.GetDirectoryAsync(
                groupId, new DefaultHttpContext(), Principal(userId), Database, CompanyAccess,
                TimeProvider.System, search, cancellationToken: default);

        public ClaimsPrincipal Principal(Guid id) => new(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim("AccountKind", "User"),
            ], "test"));

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
