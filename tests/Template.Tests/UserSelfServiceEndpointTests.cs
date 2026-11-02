using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class UserSelfServiceEndpointTests
{
    [Fact]
    public async Task MyProfile_ReturnsOnlyTheAuthenticatedUsersSafeFields()
    {
        await using var fixture = new Fixture();
        var result = await Server.Api.ApiEndpoints.GetMyProfileAsync(
            new DefaultHttpContext(),
            TenantFixture.CompanyUser(fixture.Alice.Id),
            fixture.Database,
            default);

        var ok = Assert.IsType<Ok<Server.Api.MyProfileResponse>>(result.Result);
        Assert.Equal("alice", ok.Value!.Username);
        Assert.Equal("آلیس", ok.Value.FirstName);
        Assert.Equal("مثالی", ok.Value.LastName);
        Assert.Equal("alice@example.test", ok.Value.EmailAddress);
        Assert.DoesNotContain("Password", typeof(Server.Api.MyProfileResponse).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain("RoleId", typeof(Server.Api.MyProfileResponse).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task MyProfile_DoesNotExposeInfrastructureAccountAsACompanyUserProfile()
    {
        await using var fixture = new Fixture();
        var result = await Server.Api.ApiEndpoints.GetMyProfileAsync(
            new DefaultHttpContext(),
            TenantFixture.InfrastructureAccount(),
            fixture.Database,
            default);

        Assert.IsType<UnauthorizedHttpResult>(result.Result);
    }

    [Fact]
    public async Task MyLoginHistory_ReturnsOnlyAuthenticationEventsForTheCurrentUser()
    {
        await using var fixture = new Fixture();
        fixture.Database.SecurityAuditEvents.AddRange(
            new Domain.SecurityAuditEvent
            {
                UserId = fixture.Alice.Id,
                EventType = "LoginSucceeded",
                OccurredAtUtc = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc),
                IpAddress = "192.0.2.10",
            },
            new Domain.SecurityAuditEvent
            {
                UserId = fixture.Alice.Id,
                EventType = "LoginFailed",
                OccurredAtUtc = new DateTime(2026, 11, 1, 7, 0, 0, DateTimeKind.Utc),
                IpAddress = "192.0.2.10",
            },
            new Domain.SecurityAuditEvent
            {
                UserId = fixture.Bob.Id,
                EventType = "LoginSucceeded",
                OccurredAtUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc),
                IpAddress = "192.0.2.20",
            },
            new Domain.SecurityAuditEvent
            {
                UserId = fixture.Alice.Id,
                EventType = "RoleChanged",
                OccurredAtUtc = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
            });
        await fixture.Database.SaveChangesAsync();

        var result = await Server.Api.ApiEndpoints.GetMyLoginHistoryAsync(
            new DefaultHttpContext(),
            TenantFixture.CompanyUser(fixture.Alice.Id),
            fixture.Database,
            take: 50,
            cancellationToken: default);

        var ok = Assert.IsType<Ok<IReadOnlyList<Server.Api.LoginHistoryItemResponse>>>(result.Result);
        Assert.Equal(2, ok.Value!.Count);
        Assert.All(ok.Value, item => Assert.Contains(item.EventType, new[] { "LoginSucceeded", "LoginFailed" }));
        Assert.DoesNotContain(ok.Value, item => item.IpAddress == "192.0.2.20");
    }

    [Fact]
    public async Task MyLoginHistory_ClampsTheRequestedPageSize()
    {
        await using var fixture = new Fixture();
        var result = await Server.Api.ApiEndpoints.GetMyLoginHistoryAsync(
            new DefaultHttpContext(),
            TenantFixture.CompanyUser(fixture.Alice.Id),
            fixture.Database,
            take: 0,
            cancellationToken: default);

        var ok = Assert.IsType<Ok<IReadOnlyList<Server.Api.LoginHistoryItemResponse>>>(result.Result);
        Assert.Empty(ok.Value!);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            Alice = AddUser("alice", "آلیس", "مثالی");
            Bob = AddUser("bob", "باب", "نمونه");
            Database.SaveChanges();
        }

        public Data.DatabaseContext Database { get; }
        public Domain.User Alice { get; }
        public Domain.User Bob { get; }

        private Domain.User AddUser(string username, string firstName, string lastName)
        {
            var user = new Domain.User($"{username}@example.test")
            {
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                IsActive = true,
            };
            Database.Users.Add(user);
            return user;
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}

public sealed class UserSessionAuditServiceTests
{
    [Fact]
    public async Task RecordLogoutAsync_RecordsAnExplicitLogoutForTheAuthenticatedCompanyUser()
    {
        await using var database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var user = new Domain.User("logout@example.test") { Username = "logout", IsActive = true };
        database.Users.Add(user);
        await database.SaveChangesAsync();

        var service = new Infrastructure.Security.UserSessionAuditService(database, TimeProvider.System);
        await service.RecordLogoutAsync(
            TenantFixture.CompanyUser(user.Id),
            "192.0.2.30",
            "00-test-correlation",
            default);

        var audit = await database.SecurityAuditEvents.SingleAsync();
        Assert.Equal("LogoutSucceeded", audit.EventType);
        Assert.Equal(user.Id, audit.UserId);
        Assert.Equal("192.0.2.30", audit.IpAddress);
        Assert.Equal("00-test-correlation", audit.CorrelationId);
    }

    [Fact]
    public async Task RecordLogoutAsync_DoesNotCreateCompanyUserHistoryForInfrastructureAccount()
    {
        await using var database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var service = new Infrastructure.Security.UserSessionAuditService(database, TimeProvider.System);

        await service.RecordLogoutAsync(
            TenantFixture.InfrastructureAccount(),
            "192.0.2.30",
            "00-test-correlation",
            default);

        Assert.Empty(await database.SecurityAuditEvents.ToListAsync());
    }
}
