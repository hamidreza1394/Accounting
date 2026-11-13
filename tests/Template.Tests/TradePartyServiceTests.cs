using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.Parties;
using Faranou.Modules.Trade.Domain.Parties;
using Xunit;

namespace Template.Tests;

public sealed class TradePartyServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly TradeAccessContext Context = new(UserId, TenantId, Guid.NewGuid(), true);
    private static readonly string[] OneRole = ["Importer"];

    private readonly FakePartyStore store = new();
    private readonly PartyService service;

    public TradePartyServiceTests()
    {
        service = new PartyService(store, new FixedTimeProvider());
    }

    private static CreatePartyRequest Create(
        string? code = "P-1", string? legalName = "Legal", string? displayName = null,
        string? legalIdentifier = null, string? country = null, IReadOnlyList<string?>? roles = null) =>
        new(code, legalName, displayName, legalIdentifier, country, roles ?? OneRole);

    private static UpdatePartyRequest Update(
        string rowVersion, string code = "P-1", IReadOnlyList<string?>? roles = null, string legalName = "Legal") =>
        new(code, legalName, null, null, null, roles ?? OneRole, rowVersion);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RequiresCodeAfterTrim(string code)
    {
        var result = await service.CreateAsync(Context, Create(code: code), CancellationToken.None);
        AssertInvalid(result, "code");
    }

    [Fact]
    public async Task Create_RejectsTooLongValuesWithoutEchoingThem()
    {
        var longCode = new string('c', 65);
        var longName = new string('n', 201);
        var longIdentifier = new string('i', 65);

        AssertInvalid(await service.CreateAsync(Context, Create(code: longCode), CancellationToken.None), "code", longCode);
        AssertInvalid(await service.CreateAsync(Context, Create(legalName: longName), CancellationToken.None), "legalName", longName);
        AssertInvalid(await service.CreateAsync(Context, Create(displayName: longName), CancellationToken.None), "displayName", longName);
        AssertInvalid(await service.CreateAsync(Context, Create(legalIdentifier: longIdentifier), CancellationToken.None), "legalIdentifier", longIdentifier);
        AssertInvalid(await service.CreateAsync(Context, Create(legalName: "  "), CancellationToken.None), "legalName");
        Assert.Empty(store.Parties);
    }

    [Theory]
    [InlineData("a\u0000b")]
    [InlineData("a\nb")]
    [InlineData("a\tb")]
    [InlineData("a\u007Fb")]
    [InlineData("a\u202Eb")]
    [InlineData("a\u202Ab")]
    [InlineData("a\u2066b")]
    [InlineData("a\u2069b")]
    [InlineData("a\u200Eb")]
    [InlineData("a\u200Fb")]
    public async Task Create_RejectsControlAndBidiCharactersInEveryTextField(string bad)
    {
        AssertInvalid(await service.CreateAsync(Context, Create(code: bad), CancellationToken.None), "code", bad);
        AssertInvalid(await service.CreateAsync(Context, Create(legalName: bad), CancellationToken.None), "legalName", bad);
        AssertInvalid(await service.CreateAsync(Context, Create(displayName: bad), CancellationToken.None), "displayName", bad);
        AssertInvalid(await service.CreateAsync(Context, Create(legalIdentifier: bad), CancellationToken.None), "legalIdentifier", bad);
        Assert.Empty(store.Parties);
    }

    [Fact]
    public async Task Create_AcceptsPersianTextWithZwnjAndPunctuation()
    {
        var result = await service.CreateAsync(
            Context,
            Create(code: "ط-۱۰۲", legalName: "شرکت می\u200Cخواهم بازرگانی (سهامی خاص)", displayName: "نمایشی\u200Cنام", legalIdentifier: "۱۴۰۰-۱۲۳"),
            CancellationToken.None);

        Assert.Equal(PartyOutcome.Success, result.Outcome);
        Assert.Contains("\u200C", result.Value!.LegalName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_AcceptsBoundaryLengths()
    {
        var result = await service.CreateAsync(
            Context,
            Create(code: new string('c', 64), legalName: new string('n', 200), displayName: new string('d', 200), legalIdentifier: new string('i', 64)),
            CancellationToken.None);

        Assert.Equal(PartyOutcome.Success, result.Outcome);
    }

    [Theory]
    [InlineData("T")]
    [InlineData("TUR")]
    [InlineData("T1")]
    [InlineData("ال")]
    [InlineData("QQ")]
    public async Task Create_RejectsInvalidOrUnknownCountry(string country)
    {
        AssertInvalid(await service.CreateAsync(Context, Create(country: country), CancellationToken.None), "countryCode", country);
    }

    [Fact]
    public async Task Create_UppercasesAndChecksCountry()
    {
        var result = await service.CreateAsync(Context, Create(country: " tr "), CancellationToken.None);

        Assert.Equal(PartyOutcome.Success, result.Outcome);
        Assert.Equal("TR", result.Value!.CountryCode);
    }

    [Fact]
    public async Task Create_RejectsNumericAndMalformedRoleStrings()
    {
        foreach (var role in new[] { "1", "01", "-1", "0", "99", "importer", " Importer", "Importer,Seller", "", "Nope" })
        {
            AssertInvalid(await service.CreateAsync(Context, Create(roles: [role]), CancellationToken.None), "roles", role);
        }

        AssertInvalid(await service.CreateAsync(Context, Create(roles: [null]), CancellationToken.None), "roles");
        AssertInvalid(await service.CreateAsync(Context, Create(roles: []), CancellationToken.None), "roles");
        AssertInvalid(await service.CreateAsync(Context, new CreatePartyRequest("P", "L", null, null, null, null), CancellationToken.None), "roles");
        AssertInvalid(await service.CreateAsync(Context, Create(roles: ["Importer", "Importer"]), CancellationToken.None), "roles");
        Assert.Empty(store.Parties);
    }

    [Fact]
    public async Task Create_AcceptsAllNineRolesAndRejectsMore()
    {
        var all = Enum.GetNames<PartyRole>();
        Assert.Equal(9, all.Length);

        var created = await service.CreateAsync(Context, Create(roles: all), CancellationToken.None);
        Assert.Equal(PartyOutcome.Success, created.Outcome);
        Assert.Equal(all.Order(StringComparer.Ordinal), created.Value!.Roles);

        AssertInvalid(await service.CreateAsync(Context, Create(code: "Q", roles: [.. all, "Importer"]), CancellationToken.None), "roles");
    }

    [Fact]
    public async Task Create_UsesContextTenantAndUserAndReportsDuplicateCode()
    {
        var first = await service.CreateAsync(Context, Create(code: "  D-1 "), CancellationToken.None);
        var second = await service.CreateAsync(Context, Create(code: "d-1"), CancellationToken.None);

        Assert.Equal(PartyOutcome.Success, first.Outcome);
        Assert.Equal("D-1", first.Value!.Code);
        Assert.Equal(TenantId, store.Parties.Single().TenantId);
        Assert.Equal(UserId, store.Parties.Single().CreatedByUserId);
        Assert.Equal(PartyOutcome.DuplicateCode, second.Outcome);
    }

    [Fact]
    public async Task Update_RequiresValidRowVersion()
    {
        var party = await Seed();

        foreach (var token in new string?[] { null, "", "   ", "not base64!", Convert.ToBase64String([1, 2, 3]) })
        {
            AssertInvalid(await service.UpdateAsync(Context, party.Id, Update(token!), CancellationToken.None), "rowVersion");
        }
    }

    [Fact]
    public async Task Update_ReturnsNotFoundForUnknownParty()
    {
        var result = await service.UpdateAsync(Context, Guid.NewGuid(), Update(store.TokenText(Guid.NewGuid())), CancellationToken.None);

        Assert.Equal(PartyOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_AppliesRoleDiffKeepingUnchangedRowsAndSoftDeletingRemovedOnes()
    {
        var party = await Seed(["Importer", "Seller"]);
        var tracked = store.Parties.Single();
        var importerRow = tracked.Roles.Single(role => role.Role == PartyRole.Importer);
        var sellerRow = tracked.Roles.Single(role => role.Role == PartyRole.Seller);

        var result = await service.UpdateAsync(
            Context, party.Id, Update(store.TokenText(party.Id), roles: ["Importer", "Carrier"]), CancellationToken.None);

        Assert.Equal(PartyOutcome.Success, result.Outcome);
        Assert.Equal(["Carrier", "Importer"], result.Value!.Roles);
        Assert.Same(importerRow, tracked.Roles.Single(role => role.Role == PartyRole.Importer));
        Assert.False(importerRow.IsDeleted);
        Assert.True(sellerRow.IsDeleted);
        Assert.Equal(3, tracked.Roles.Count);
        Assert.Single(tracked.Roles, role => role.Role == PartyRole.Carrier && !role.IsDeleted);
    }

    [Fact]
    public async Task Update_RemovingThenReAddingARoleInSeparateUpdatesAddsAFreshActiveRow()
    {
        var party = await Seed(["Importer", "Seller"]);
        var removed = await service.UpdateAsync(Context, party.Id, Update(store.TokenText(party.Id), roles: ["Importer"]), CancellationToken.None);
        var readded = await service.UpdateAsync(Context, party.Id, Update(store.TokenText(party.Id), roles: ["Importer", "Seller"]), CancellationToken.None);

        Assert.Equal(["Importer"], removed.Value!.Roles);
        Assert.Equal(["Importer", "Seller"], readded.Value!.Roles);
        Assert.Equal(2, store.Parties.Single().Roles.Count(role => role.Role == PartyRole.Seller));
        Assert.Single(store.Parties.Single().Roles, role => role.Role == PartyRole.Seller && !role.IsDeleted);
    }

    [Fact]
    public async Task Update_UnchangedRolesAddNoRows()
    {
        var party = await Seed(["Importer", "Seller"]);

        var result = await service.UpdateAsync(
            Context, party.Id, Update(store.TokenText(party.Id), roles: ["Seller", "Importer"]), CancellationToken.None);

        Assert.Equal(PartyOutcome.Success, result.Outcome);
        Assert.Equal(2, store.Parties.Single().Roles.Count);
    }

    [Fact]
    public async Task Update_NoOpStillComparesRowVersionAndStaleTokenConflicts()
    {
        var party = await Seed();
        var current = store.TokenText(party.Id);

        var noOp = await service.UpdateAsync(Context, party.Id, Update(current), CancellationToken.None);
        Assert.Equal(PartyOutcome.Success, noOp.Outcome);
        Assert.Equal(Convert.FromBase64String(current), store.LastOriginalRowVersion);

        var stale = await service.UpdateAsync(Context, party.Id, Update(current), CancellationToken.None);
        Assert.Equal(PartyOutcome.ConcurrencyConflict, stale.Outcome);
    }

    [Fact]
    public async Task Update_DuplicateCodeOfAnotherPartyConflictsButOwnCodeAndCaseChangeAreAllowed()
    {
        await Seed(code: "A-1");
        var second = await Seed(code: "B-1");

        var clash = await service.UpdateAsync(Context, second.Id, Update(store.TokenText(second.Id), code: "a-1"), CancellationToken.None);
        var ownCase = await service.UpdateAsync(Context, second.Id, Update(store.TokenText(second.Id), code: "b-1"), CancellationToken.None);

        Assert.Equal(PartyOutcome.DuplicateCode, clash.Outcome);
        Assert.Equal(PartyOutcome.Success, ownCase.Outcome);
    }

    [Fact]
    public async Task List_ItemsCarryNoLegalIdentifierAndDetailDoes()
    {
        var created = await service.CreateAsync(Context, Create(legalIdentifier: "SECRET-ID"), CancellationToken.None);
        var list = await service.ListAsync(Context, new PartyListQuery(null, null, null, 1, 20), CancellationToken.None);

        Assert.DoesNotContain(typeof(PartyListItem).GetProperties(), property => property.Name.Contains("Identifier", StringComparison.Ordinal));
        Assert.Equal("SECRET-ID", created.Value!.LegalIdentifier);
        Assert.Single(list.Value!.Items);
    }

    [Fact]
    public async Task List_ValidatesSearchAndRoleAndClampsPaging()
    {
        AssertInvalid(await service.ListAsync(Context, new PartyListQuery(new string('s', 101), null, null, 1, 20), CancellationToken.None), "search");
        AssertInvalid(await service.ListAsync(Context, new PartyListQuery(null, "1", null, 1, 20), CancellationToken.None), "role");
        AssertInvalid(await service.ListAsync(Context, new PartyListQuery(null, "importer", null, 1, 20), CancellationToken.None), "role");

        var ok = await service.ListAsync(Context, new PartyListQuery("  x  ", "Seller", true, -5, 5000), CancellationToken.None);
        Assert.Equal(1, ok.Value!.Page);
        Assert.Equal(100, ok.Value.PageSize);
        Assert.Equal("x", store.LastFilter!.Search);
        Assert.Equal(PartyRole.Seller, store.LastFilter.Role);
        Assert.Equal(0, store.LastFilter.Skip);

        var small = await service.ListAsync(Context, new PartyListQuery(null, null, null, 3, 0), CancellationToken.None);
        Assert.Equal(1, small.Value!.PageSize);
        Assert.Equal(2, store.LastFilter!.Skip);

        var blankSearch = await service.ListAsync(Context, new PartyListQuery("   ", null, null, 1, 20), CancellationToken.None);
        Assert.Null(store.LastFilter!.Search);
        Assert.Equal(PartyOutcome.Success, blankSearch.Outcome);
    }

    private async Task<PartyDetail> Seed(IReadOnlyList<string?>? roles = null, string code = "P-1")
    {
        var result = await service.CreateAsync(Context, Create(code: code, roles: roles), CancellationToken.None);
        Assert.Equal(PartyOutcome.Success, result.Outcome);
        return result.Value!;
    }

    private static void AssertInvalid<T>(PartyResult<T> result, string key, string? sentValue = null)
    {
        Assert.Equal(PartyOutcome.ValidationFailed, result.Outcome);
        Assert.True(result.Errors!.ContainsKey(key), $"Expected error key '{key}', got: {string.Join(",", result.Errors.Keys)}.");
        if (!string.IsNullOrEmpty(sentValue) && sentValue.Length > 1)
        {
            Assert.DoesNotContain(result.Errors.Values.SelectMany(messages => messages), message => message.Contains(sentValue, StringComparison.Ordinal));
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakePartyStore : IPartyStore
    {
        private readonly Dictionary<Guid, byte[]> tokens = [];
        private long counter;

        public List<Party> Parties { get; } = [];

        public byte[]? LastOriginalRowVersion { get; private set; }

        public PartyListFilter? LastFilter { get; private set; }

        public string TokenText(Guid id) => tokens.TryGetValue(id, out var token)
            ? Convert.ToBase64String(token)
            : Convert.ToBase64String(BitConverter.GetBytes(0L));

        public Task<PartyPage> ListAsync(Guid tenantId, PartyListFilter filter, CancellationToken cancellationToken)
        {
            LastFilter = filter;
            var items = Parties.Where(party => party.TenantId == tenantId).ToArray();
            return Task.FromResult(new PartyPage(items, items.Length));
        }

        public Task<Party?> FindAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken) =>
            Task.FromResult(Parties.SingleOrDefault(party => party.Id == partyId && party.TenantId == tenantId));

        public Task<Party?> FindForUpdateAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken) =>
            FindAsync(tenantId, partyId, cancellationToken);

        public Task<bool> ActiveCodeExistsAsync(Guid tenantId, string code, Guid? excludingPartyId, CancellationToken cancellationToken) =>
            Task.FromResult(Parties.Any(party => party.TenantId == tenantId && party.Id != excludingPartyId &&
                string.Equals(party.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task<PartySaveOutcome> AddAsync(Party party, CancellationToken cancellationToken)
        {
            Parties.Add(party);
            Bump(party.Id);
            return Task.FromResult(PartySaveOutcome.Saved);
        }

        public Task<PartySaveOutcome> SaveChangesAsync(Party party, byte[] originalRowVersion, CancellationToken cancellationToken)
        {
            LastOriginalRowVersion = originalRowVersion;
            if (!tokens[party.Id].SequenceEqual(originalRowVersion))
            {
                return Task.FromResult(PartySaveOutcome.ConcurrencyConflict);
            }

            Bump(party.Id);
            return Task.FromResult(PartySaveOutcome.Saved);
        }

        private void Bump(Guid id) => tokens[id] = BitConverter.GetBytes(++counter);
    }
}
