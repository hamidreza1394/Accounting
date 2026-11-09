using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Server.Api;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The personnel photo of a company employee: company scope, permission split (view reads, update
/// writes), signature-based type check, size limit, one photo per employee, audit, removal.
/// </summary>
public sealed class HrEmployeePhotoEndpointTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
    private static readonly byte[] Webp = [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P', 1];

    [Theory]
    [MemberData(nameof(SupportedImages))]
    public async Task Put_StoresTheSupportedImageWithTheTypeDetectedFromItsContent(byte[] bytes, string expectedType)
    {
        await using var fixture = new Fixture();
        var (actor, employee) = await fixture.ArrangeAsync(HrOrganizationalStructureEndpoints.UpdateEmployeesPermission);

        var result = await fixture.PutAsync(actor.Id, employee.Id, bytes);

        Assert.IsType<NoContent>(result);
        var stored = await fixture.Database.CompanyEmployeePhotos.SingleAsync();
        Assert.Equal(expectedType, stored.ContentType);
        Assert.Equal(bytes, stored.Content);
        Assert.Equal(fixture.Company.Id, stored.TenantId);
        Assert.Contains(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "HrEmployeePhotoSet" && audit.EntityId == employee.Id.ToString());
    }

    public static TheoryData<byte[], string> SupportedImages() => new()
    {
        { Png, "image/png" },
        { Jpeg, "image/jpeg" },
        { Webp, "image/webp" },
    };

    [Fact]
    public async Task Put_RefusesAFileThatIsNotASupportedImage_EvenWhenClaimingAnImageType()
    {
        await using var fixture = new Fixture();
        var (actor, employee) = await fixture.ArrangeAsync(HrOrganizationalStructureEndpoints.UpdateEmployeesPermission);
        // An SVG/HTML payload and a GIF are both outside the allowed signatures.
        foreach (var payload in new[] { "<svg onload=alert(1)></svg>"u8.ToArray(), "GIF89a...."u8.ToArray(), "plain text"u8.ToArray() })
        {
            var result = await fixture.PutAsync(actor.Id, employee.Id, payload, "image/png");
            Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
        }

        Assert.Empty(await fixture.Database.CompanyEmployeePhotos.ToListAsync());
    }

    [Fact]
    public async Task Put_RefusesAnEmptyBody()
    {
        await using var fixture = new Fixture();
        var (actor, employee) = await fixture.ArrangeAsync(HrOrganizationalStructureEndpoints.UpdateEmployeesPermission);

        var result = await fixture.PutAsync(actor.Id, employee.Id, []);

        Assert.Equal(StatusCodes.Status400BadRequest, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Put_RefusesAFileLargerThanTheLimit_AndAcceptsOneExactlyAtTheLimit()
    {
        await using var fixture = new Fixture();
        var (actor, employee) = await fixture.ArrangeAsync(HrOrganizationalStructureEndpoints.UpdateEmployeesPermission);
        var atLimit = new byte[HrEmployeePhotoEndpoints.MaxPhotoBytes];
        Png.CopyTo(atLimit, 0);
        var overLimit = new byte[HrEmployeePhotoEndpoints.MaxPhotoBytes + 1];
        Png.CopyTo(overLimit, 0);

        var tooLarge = await fixture.PutAsync(actor.Id, employee.Id, overLimit);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, (tooLarge as IStatusCodeHttpResult)?.StatusCode);
        Assert.Empty(await fixture.Database.CompanyEmployeePhotos.ToListAsync());

        var accepted = await fixture.PutAsync(actor.Id, employee.Id, atLimit);
        Assert.IsType<NoContent>(accepted);
    }

    [Fact]
    public async Task Put_ReplacesTheExistingPhoto_KeepingOneRowPerEmployee()
    {
        await using var fixture = new Fixture();
        var (actor, employee) = await fixture.ArrangeAsync(HrOrganizationalStructureEndpoints.UpdateEmployeesPermission);

        await fixture.PutAsync(actor.Id, employee.Id, Png);
        await fixture.PutAsync(actor.Id, employee.Id, Jpeg);

        var stored = await fixture.Database.CompanyEmployeePhotos.SingleAsync();
        Assert.Equal("image/jpeg", stored.ContentType);
    }

    [Fact]
    public async Task Put_NeedsTheUpdatePermission_AViewOnlyMemberIsForbidden()
    {
        await using var fixture = new Fixture();
        var (viewer, employee) = await fixture.ArrangeAsync(HrOrganizationalStructureEndpoints.ViewEmployeesPermission);

        var result = await fixture.PutAsync(viewer.Id, employee.Id, Png);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Empty(await fixture.Database.CompanyEmployeePhotos.ToListAsync());
    }

    [Fact]
    public async Task Get_ReturnsTheStoredBytesToAViewer_And404WhenThereIsNone()
    {
        await using var fixture = new Fixture();
        var (writer, employee) = await fixture.ArrangeAsync(
            HrOrganizationalStructureEndpoints.UpdateEmployeesPermission,
            HrOrganizationalStructureEndpoints.ViewEmployeesPermission);

        Assert.IsType<NotFound>(await fixture.GetAsync(writer.Id, employee.Id));
        await fixture.PutAsync(writer.Id, employee.Id, Png);

        var result = await fixture.GetAsync(writer.Id, employee.Id);
        var file = Assert.IsType<FileContentHttpResult>(result);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(Png, file.FileContents.ToArray());
    }

    [Fact]
    public async Task Get_WithoutTheViewPermission_IsForbidden()
    {
        await using var fixture = new Fixture();
        var (updater, employee) = await fixture.ArrangeAsync(HrOrganizationalStructureEndpoints.UpdateEmployeesPermission);
        await fixture.PutAsync(updater.Id, employee.Id, Png);

        var result = await fixture.GetAsync(updater.Id, employee.Id);

        Assert.Equal(StatusCodes.Status403Forbidden, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task Photo_OfACompanyIsInvisibleAndImmutableFromAnotherCompany()
    {
        await using var fixture = new Fixture();
        var (actor, employee) = await fixture.ArrangeAsync(
            HrOrganizationalStructureEndpoints.UpdateEmployeesPermission,
            HrOrganizationalStructureEndpoints.ViewEmployeesPermission);
        await fixture.PutAsync(actor.Id, employee.Id, Png);
        var other = fixture.AddCompany("شرکت دوم");
        var outsider = await fixture.AddMemberAsync(other,
            HrOrganizationalStructureEndpoints.UpdateEmployeesPermission,
            HrOrganizationalStructureEndpoints.ViewEmployeesPermission);

        // The outsider is a member of ANOTHER company: addressing this company's employee is a 404,
        // and addressing it through their own company finds no such employee either.
        Assert.IsType<NotFound>(await fixture.GetAsync(outsider.Id, employee.Id));
        Assert.IsType<NotFound>(await fixture.PutAsync(outsider.Id, employee.Id, Jpeg));
        Assert.IsType<NotFound>(await fixture.DeleteAsync(outsider.Id, employee.Id));
        Assert.IsType<NotFound>(await fixture.GetAsync(outsider.Id, employee.Id, other.Id));
        Assert.IsType<NotFound>(await fixture.PutAsync(outsider.Id, employee.Id, Jpeg, companyId: other.Id));

        var stored = await fixture.Database.CompanyEmployeePhotos.SingleAsync();
        Assert.Equal("image/png", stored.ContentType);
    }

    [Fact]
    public async Task Delete_RemovesThePhotoAndAudits_AndIsIdempotent()
    {
        await using var fixture = new Fixture();
        var (actor, employee) = await fixture.ArrangeAsync(HrOrganizationalStructureEndpoints.UpdateEmployeesPermission);
        await fixture.PutAsync(actor.Id, employee.Id, Png);

        Assert.IsType<NoContent>(await fixture.DeleteAsync(actor.Id, employee.Id));
        Assert.IsType<NoContent>(await fixture.DeleteAsync(actor.Id, employee.Id));

        Assert.Empty(await fixture.Database.CompanyEmployeePhotos.ToListAsync());
        Assert.Contains(await fixture.Database.SecurityAuditEvents.ToListAsync(),
            audit => audit.EventType == "HrEmployeePhotoRemoved" && audit.EntityId == employee.Id.ToString());
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg")]
    [InlineData(new byte[] { 0xFF, 0xD8 }, null)]
    [InlineData(new byte[] { }, null)]
    public void DetectImageContentType_ReadsOnlyTheFileSignature(byte[] bytes, string? expected) =>
        Assert.Equal(expected, HrEmployeePhotoEndpoints.DetectImageContentType(bytes));

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Company = AddCompany("شرکت نمونه");
        }

        public Data.DatabaseContext Database { get; }
        public Domain.Company Company { get; }
        private Infrastructure.Tenancy.CompanyAccessService CompanyAccess => new(Database);

        public Domain.Company AddCompany(string name)
        {
            var company = new Domain.Company { Name = name, IsActive = true };
            Database.Companies.Add(company);
            return company;
        }

        public async Task<(Domain.User Actor, Domain.CompanyEmployee Employee)> ArrangeAsync(params string[] permissions)
        {
            var actor = await AddMemberAsync(Company, permissions);
            var employee = new Domain.CompanyEmployee
            {
                TenantId = Company.Id,
                EmployeeNumber = "EMP-1",
                FirstName = "علی",
                LastName = "محمدی",
                CreatedByUserId = actor.Id,
            };
            Database.CompanyEmployees.Add(employee);
            await Database.SaveChangesAsync();
            return (actor, employee);
        }

        public async Task<Domain.User> AddMemberAsync(Domain.Company company, params string[] permissions)
        {
            var role = new Domain.CompanyRole
            {
                TenantId = company.Id,
                Company = company,
                Code = "R-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "نقش",
                IsActive = true,
            };
            Database.CompanyRoles.Add(role);
            foreach (var code in permissions)
            {
                var permission = Database.PermissionDefinitions.Local.FirstOrDefault(current => current.Code == code)
                    ?? await Database.PermissionDefinitions.FirstOrDefaultAsync(current => current.Code == code);
                if (permission is null)
                {
                    permission = new Domain.PermissionDefinition { Code = code, Name = code, Description = "test" };
                    Database.PermissionDefinitions.Add(permission);
                }

                Database.CompanyRolePermissions.Add(new Domain.CompanyRolePermission { Role = role, PermissionId = permission.Id });
            }

            var user = new Domain.User($"u-{Guid.NewGuid():N}@example.test")
            {
                Username = "u" + Guid.NewGuid().ToString("N")[..8],
                FirstName = "عضو",
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
            await Database.SaveChangesAsync();
            return user;
        }

        public Task<IResult> PutAsync(Guid actorId, Guid employeeId, byte[] bytes, string? contentType = null, Guid? companyId = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = null;
            if (contentType is not null) context.Request.ContentType = contentType;
            return HrEmployeePhotoEndpoints.PutPhotoAsync(
                companyId ?? Company.Id, employeeId, context, Principal(actorId), new AllowAntiforgery(),
                Database, CompanyAccess, TimeProvider.System, default);
        }

        public Task<IResult> GetAsync(Guid actorId, Guid employeeId, Guid? companyId = null) =>
            HrEmployeePhotoEndpoints.GetPhotoAsync(
                companyId ?? Company.Id, employeeId, new DefaultHttpContext(), Principal(actorId),
                Database, CompanyAccess, default);

        public Task<IResult> DeleteAsync(Guid actorId, Guid employeeId, Guid? companyId = null) =>
            HrEmployeePhotoEndpoints.DeletePhotoAsync(
                companyId ?? Company.Id, employeeId, new DefaultHttpContext(), Principal(actorId),
                new AllowAntiforgery(), Database, CompanyAccess, TimeProvider.System, default);

        private static ClaimsPrincipal Principal(Guid id) => new(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim("AccountKind", "User"),
            ], "test"));

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }

    private sealed class AllowAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
