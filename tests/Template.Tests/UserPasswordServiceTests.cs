using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Template.Tests;

public sealed class UserPasswordServiceTests
{
    [Fact]
    public async Task ChangePasswordAsync_WithValidCurrentPassword_UpdatesCredentialAndAuditsChange()
    {
        await using var fixture = new Fixture();
        var result = await fixture.Service.ChangePasswordAsync(
            fixture.User.Id,
            "Old-Password-1405!",
            "New-Long-Phrase-1406!",
            "New-Long-Phrase-1406!",
            "192.0.2.10");

        Assert.Equal(Infrastructure.Security.ChangePasswordOutcome.Changed, result.Outcome);
        var updated = await fixture.Database.Users.SingleAsync(user => user.Id == fixture.User.Id);
        Assert.Equal(
            Infrastructure.Security.PasswordVerificationOutcome.Success,
            fixture.Hasher.Verify(updated.Password, "New-Long-Phrase-1406!"));
        Assert.Equal(
            Infrastructure.Security.PasswordVerificationOutcome.Failed,
            fixture.Hasher.Verify(updated.Password, "Old-Password-1405!"));

        var audit = await fixture.Database.SecurityAuditEvents.SingleAsync();
        Assert.Equal("PasswordChanged", audit.EventType);
        Assert.Equal(fixture.User.Id, audit.UserId);
        Assert.Equal("192.0.2.10", audit.IpAddress);
        Assert.DoesNotContain("New-Long-Phrase-1406!", audit.DetailsJson ?? string.Empty);
        Assert.DoesNotContain("Old-Password-1405!", audit.DetailsJson ?? string.Empty);
    }

    [Fact]
    public async Task ChangePasswordAsync_RevokesEveryActiveSessionOfTheUser()
    {
        await using var fixture = new Fixture();
        var now = DateTime.UtcNow;
        var active = new Domain.UserSession { UserId = fixture.User.Id, CreatedAtUtc = now, LastSeenAtUtc = now };
        var alreadyRevoked = new Domain.UserSession
        {
            UserId = fixture.User.Id,
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            RevokedAtUtc = now,
            RevocationReasonCode = "self_revoked",
        };
        fixture.Database.UserSessions.AddRange(active, alreadyRevoked);
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.Service.ChangePasswordAsync(
            fixture.User.Id,
            "Old-Password-1405!",
            "New-Long-Phrase-1406!",
            "New-Long-Phrase-1406!",
            "192.0.2.10");

        Assert.Equal(Infrastructure.Security.ChangePasswordOutcome.Changed, result.Outcome);
        var revoked = await fixture.Database.UserSessions.SingleAsync(session => session.Id == active.Id);
        Assert.NotNull(revoked.RevokedAtUtc);
        Assert.Equal("password_changed", revoked.RevocationReasonCode);
        var untouched = await fixture.Database.UserSessions.SingleAsync(session => session.Id == alreadyRevoked.Id);
        Assert.Equal("self_revoked", untouched.RevocationReasonCode);
        var audit = await fixture.Database.SecurityAuditEvents.SingleAsync();
        Assert.Contains("revokedSessions", audit.DetailsJson ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_DoesNotChangeCredentialOrAudit()
    {
        await using var fixture = new Fixture();
        var oldHash = fixture.User.Password;

        var result = await fixture.Service.ChangePasswordAsync(
            fixture.User.Id,
            "Wrong-Password-1405!",
            "New-Long-Phrase-1406!",
            "New-Long-Phrase-1406!",
            "192.0.2.10");

        Assert.Equal(Infrastructure.Security.ChangePasswordOutcome.CurrentPasswordInvalid, result.Outcome);
        var unchanged = await fixture.Database.Users.SingleAsync(user => user.Id == fixture.User.Id);
        Assert.Equal(oldHash, unchanged.Password);
        Assert.Empty(await fixture.Database.SecurityAuditEvents.ToListAsync());
    }

    [Fact]
    public async Task ChangePasswordAsync_WithMismatchedConfirmation_IsRejected()
    {
        await using var fixture = new Fixture();

        var result = await fixture.Service.ChangePasswordAsync(
            fixture.User.Id,
            "Old-Password-1405!",
            "New-Long-Phrase-1406!",
            "Different-Long-Phrase-1406!",
            "192.0.2.10");

        Assert.Equal(Infrastructure.Security.ChangePasswordOutcome.ConfirmationMismatch, result.Outcome);
        Assert.Empty(await fixture.Database.SecurityAuditEvents.ToListAsync());
    }

    [Theory]
    [InlineData("short")]
    [InlineData("password123")]
    [InlineData("Zq9!mK2")] // D-078: seven characters, one below the minimum 8.
    public async Task ChangePasswordAsync_RejectsShortOrCommonPasswords(string newPassword)
    {
        await using var fixture = new Fixture();

        var result = await fixture.Service.ChangePasswordAsync(
            fixture.User.Id,
            "Old-Password-1405!",
            newPassword,
            newPassword,
            "192.0.2.10");

        Assert.Equal(Infrastructure.Security.ChangePasswordOutcome.PasswordPolicyRejected, result.Outcome);
        Assert.Empty(await fixture.Database.SecurityAuditEvents.ToListAsync());
    }

    [Fact]
    public async Task ChangePasswordAsync_AcceptsAPasswordOfExactlyTheMinimumLength()
    {
        await using var fixture = new Fixture();

        // D-078: the boundary - exactly 8 characters is accepted.
        var password = "Zq9!mK2x";
        Assert.Equal(Constants.MinLength.UserPassword, password.Length);

        var result = await fixture.Service.ChangePasswordAsync(
            fixture.User.Id,
            "Old-Password-1405!",
            password,
            password,
            "192.0.2.10");

        Assert.Equal(Infrastructure.Security.ChangePasswordOutcome.Changed, result.Outcome);
    }

    [Fact]
    public async Task ChangePasswordAsync_AcceptsALongPassphrase_WithSpaces()
    {
        await using var fixture = new Fixture();

        // D-073 §5/D-078: spaces and passphrases are allowed, no composition rules.
        var result = await fixture.Service.ChangePasswordAsync(
            fixture.User.Id,
            "Old-Password-1405!",
            "correct horse battery staple 42!",
            "correct horse battery staple 42!",
            "192.0.2.10");

        Assert.Equal(Infrastructure.Security.ChangePasswordOutcome.Changed, result.Outcome);
    }

    [Fact]
    public async Task ChangePasswordAsync_RejectsReusingCurrentPassword()
    {
        await using var fixture = new Fixture();

        var result = await fixture.Service.ChangePasswordAsync(
            fixture.User.Id,
            "Old-Password-1405!",
            "Old-Password-1405!",
            "Old-Password-1405!",
            "192.0.2.10");

        Assert.Equal(Infrastructure.Security.ChangePasswordOutcome.NewPasswordMustDiffer, result.Outcome);
        Assert.Empty(await fixture.Database.SecurityAuditEvents.ToListAsync());
    }

    [Fact]
    public async Task ChangePasswordAsync_DoesNotAllowInactiveAccounts()
    {
        await using var fixture = new Fixture(isActive: false);

        var result = await fixture.Service.ChangePasswordAsync(
            fixture.User.Id,
            "Old-Password-1405!",
            "New-Long-Phrase-1406!",
            "New-Long-Phrase-1406!",
            "192.0.2.10");

        Assert.Equal(Infrastructure.Security.ChangePasswordOutcome.AccountUnavailable, result.Outcome);
        Assert.Empty(await fixture.Database.SecurityAuditEvents.ToListAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(bool isActive = true)
        {
            var options = new DbContextOptionsBuilder<Data.DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Database = new Data.DatabaseContext(options);
            Hasher = new Infrastructure.Security.PasswordHashService();
            User = new Domain.User("self-service@example.test")
            {
                Username = "self-service",
                IsActive = isActive,
                Password = Hasher.Hash("Old-Password-1405!"),
            };
            Database.Users.Add(User);
            Database.SaveChanges();
            Service = new Infrastructure.Security.UserPasswordService(
                Database,
                Hasher,
                TimeProvider.System);
        }

        public Data.DatabaseContext Database { get; }
        public Infrastructure.Security.PasswordHashService Hasher { get; }
        public Domain.User User { get; }
        public Infrastructure.Security.UserPasswordService Service { get; }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
