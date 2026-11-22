using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.Parties;
using Faranou.Modules.Trade.Application.Products;
using Faranou.Modules.Trade.Domain.Parties;
using Faranou.Modules.Trade.Domain.Products;
using Xunit;

namespace Template.Tests;

/// <summary>
/// Service-level Company-isolation and no-mutation-on-conflict checks for Party and Product (existing behavior only).
/// The stores here are in-memory: they prove the services pass the context Company to every store call and never
/// touch another Company's record. Real SQL/HTTP isolation stays covered by the SQL Server gated HTTP tests.
/// </summary>
public sealed class TradeMasterDataCompanyIsolationServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly TradeAccessContext CompanyA = new(UserId, Guid.NewGuid(), Guid.NewGuid(), true);
    private static readonly TradeAccessContext CompanyB = new(UserId, Guid.NewGuid(), Guid.NewGuid(), true);

    private readonly FakePartyStore partyStore = new();
    private readonly FakeProductStore productStore = new();
    private readonly PartyService parties;
    private readonly ProductService products;

    public TradeMasterDataCompanyIsolationServiceTests()
    {
        parties = new PartyService(partyStore, new FixedTimeProvider());
        products = new ProductService(productStore, new FixedTimeProvider());
    }

    private static CreatePartyRequest NewParty(string code, string legalName = "Legal") => new(code, legalName, null, null, null, ["Importer"]);

    private static UpdatePartyRequest PartyUpdate(string rowVersion, string code, string legalName) =>
        new(code, legalName, null, null, null, ["Importer"], rowVersion);

    private static CreateProductRequest NewProduct(string code, string name = "Steel") => new(code, name, null, null, "TON");

    private static UpdateProductRequest ProductUpdate(string rowVersion, string code, string name) =>
        new(code, name, null, null, "TON", rowVersion);

    [Fact]
    public async Task Party_UpdateAndGetOfAnotherCompanysRecord_AreNotFoundAndLeaveItUnchanged()
    {
        var foreign = (await parties.CreateAsync(CompanyA, NewParty("A-1", "Company A legal"), CancellationToken.None)).Value!;
        var tokenBefore = partyStore.TokenText(foreign.Id);

        var update = await parties.UpdateAsync(CompanyB, foreign.Id, PartyUpdate(tokenBefore, "HIJACK", "Hijacked"), CancellationToken.None);
        var get = await parties.GetAsync(CompanyB, foreign.Id, CancellationToken.None);

        Assert.Equal(PartyOutcome.NotFound, update.Outcome);
        Assert.Equal(PartyOutcome.NotFound, get.Outcome);
        Assert.Null(update.Value);
        var stored = partyStore.Parties.Single();
        Assert.Equal("A-1", stored.Code);
        Assert.Equal("Company A legal", stored.LegalName);
        Assert.Equal(tokenBefore, partyStore.TokenText(foreign.Id));
        Assert.Null(partyStore.LastOriginalRowVersion);
        Assert.All(partyStore.TenantsSeen.Skip(1), tenant => Assert.Equal(CompanyB.TenantId, tenant));
    }

    [Fact]
    public async Task Product_UpdateOfAnotherCompanysRecord_IsNotFoundAndLeavesItUnchanged()
    {
        var foreign = (await products.CreateAsync(CompanyA, NewProduct("A-1", "Company A steel"), CancellationToken.None)).Value!;
        var tokenBefore = productStore.TokenText(foreign.Id);

        var update = await products.UpdateAsync(CompanyB, foreign.Id, ProductUpdate(tokenBefore, "HIJACK", "Hijacked"), CancellationToken.None);

        Assert.Equal(ProductOutcome.NotFound, update.Outcome);
        Assert.Null(update.Value);
        var stored = productStore.Products.Single();
        Assert.Equal("A-1", stored.Code);
        Assert.Equal("Company A steel", stored.Name);
        Assert.Equal(tokenBefore, productStore.TokenText(foreign.Id));
        Assert.Null(productStore.LastOriginalRowVersion);
        Assert.All(productStore.TenantsSeen.Skip(1), tenant => Assert.Equal(CompanyB.TenantId, tenant));
    }

    [Fact]
    public async Task Party_SameCodeInTwoCompaniesIsAllowedWhileTheSameCompanyStillConflicts()
    {
        var inA = await parties.CreateAsync(CompanyA, NewParty("SHARED-1"), CancellationToken.None);
        var inB = await parties.CreateAsync(CompanyB, NewParty("shared-1"), CancellationToken.None);
        var againInA = await parties.CreateAsync(CompanyA, NewParty("Shared-1"), CancellationToken.None);

        Assert.Equal(PartyOutcome.Success, inA.Outcome);
        Assert.Equal(PartyOutcome.Success, inB.Outcome);
        Assert.Equal(PartyOutcome.DuplicateCode, againInA.Outcome);
        Assert.Equal([CompanyA.TenantId, CompanyB.TenantId], partyStore.Parties.Select(p => p.TenantId));
    }

    [Fact]
    public async Task Product_SameCodeInTwoCompaniesIsAllowedWhileTheSameCompanyStillConflicts()
    {
        var inA = await products.CreateAsync(CompanyA, NewProduct("SHARED-1"), CancellationToken.None);
        var inB = await products.CreateAsync(CompanyB, NewProduct("shared-1"), CancellationToken.None);
        var againInA = await products.CreateAsync(CompanyA, NewProduct("Shared-1"), CancellationToken.None);

        Assert.Equal(ProductOutcome.Success, inA.Outcome);
        Assert.Equal(ProductOutcome.Success, inB.Outcome);
        Assert.Equal(ProductOutcome.DuplicateCode, againInA.Outcome);
        Assert.Equal([CompanyA.TenantId, CompanyB.TenantId], productStore.Products.Select(p => p.TenantId));
    }

    [Fact]
    public async Task Party_UpdateDuplicateCodeConflict_LeavesStoredRecordAndTokenUnchanged()
    {
        await parties.CreateAsync(CompanyA, NewParty("A-1"), CancellationToken.None);
        var second = (await parties.CreateAsync(CompanyA, NewParty("B-1", "Second legal"), CancellationToken.None)).Value!;
        var tokenBefore = partyStore.TokenText(second.Id);

        var clash = await parties.UpdateAsync(CompanyA, second.Id, PartyUpdate(tokenBefore, "a-1", "Renamed"), CancellationToken.None);

        Assert.Equal(PartyOutcome.DuplicateCode, clash.Outcome);
        var stored = partyStore.Parties.Single(p => p.Id == second.Id);
        Assert.Equal("B-1", stored.Code);
        Assert.Equal("Second legal", stored.LegalName);
        Assert.Equal(tokenBefore, partyStore.TokenText(second.Id));
        Assert.Null(partyStore.LastOriginalRowVersion);
    }

    [Fact]
    public async Task Product_UpdateDuplicateCodeConflict_LeavesStoredRecordAndTokenUnchanged()
    {
        await products.CreateAsync(CompanyA, NewProduct("A-1"), CancellationToken.None);
        var second = (await products.CreateAsync(CompanyA, NewProduct("B-1", "Second steel"), CancellationToken.None)).Value!;
        var tokenBefore = productStore.TokenText(second.Id);

        var clash = await products.UpdateAsync(CompanyA, second.Id, ProductUpdate(tokenBefore, "a-1", "Renamed"), CancellationToken.None);

        Assert.Equal(ProductOutcome.DuplicateCode, clash.Outcome);
        var stored = productStore.Products.Single(p => p.Id == second.Id);
        Assert.Equal("B-1", stored.Code);
        Assert.Equal("Second steel", stored.Name);
        Assert.Equal(tokenBefore, productStore.TokenText(second.Id));
        Assert.Null(productStore.LastOriginalRowVersion);
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

        /// <summary>Every Company id the service handed to the store, in call order.</summary>
        public List<Guid> TenantsSeen { get; } = [];

        public byte[]? LastOriginalRowVersion { get; private set; }

        public string TokenText(Guid id) => Convert.ToBase64String(tokens[id]);

        public Task<PartyPage> ListAsync(Guid tenantId, PartyListFilter filter, CancellationToken cancellationToken)
        {
            TenantsSeen.Add(tenantId);
            var items = Parties.Where(party => party.TenantId == tenantId).ToArray();
            return Task.FromResult(new PartyPage(items, items.Length));
        }

        public Task<Party?> FindAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken)
        {
            TenantsSeen.Add(tenantId);
            return Task.FromResult(Parties.SingleOrDefault(party => party.Id == partyId && party.TenantId == tenantId));
        }

        public Task<Party?> FindForUpdateAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken) =>
            FindAsync(tenantId, partyId, cancellationToken);

        public Task<bool> ActiveCodeExistsAsync(Guid tenantId, string code, Guid? excludingPartyId, CancellationToken cancellationToken)
        {
            TenantsSeen.Add(tenantId);
            return Task.FromResult(Parties.Any(party => party.TenantId == tenantId && party.Id != excludingPartyId &&
                string.Equals(party.Code, code, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<PartySaveOutcome> AddAsync(Party party, CancellationToken cancellationToken)
        {
            Parties.Add(party);
            tokens[party.Id] = BitConverter.GetBytes(++counter);
            return Task.FromResult(PartySaveOutcome.Saved);
        }

        public Task<PartySaveOutcome> SaveChangesAsync(Party party, byte[] originalRowVersion, CancellationToken cancellationToken)
        {
            LastOriginalRowVersion = originalRowVersion;
            if (!tokens[party.Id].SequenceEqual(originalRowVersion))
            {
                return Task.FromResult(PartySaveOutcome.ConcurrencyConflict);
            }

            tokens[party.Id] = BitConverter.GetBytes(++counter);
            return Task.FromResult(PartySaveOutcome.Saved);
        }
    }

    private sealed class FakeProductStore : IProductStore
    {
        private readonly Dictionary<Guid, byte[]> tokens = [];
        private long counter;

        public List<Product> Products { get; } = [];

        /// <summary>Every Company id the service handed to the store, in call order.</summary>
        public List<Guid> TenantsSeen { get; } = [];

        public byte[]? LastOriginalRowVersion { get; private set; }

        public string TokenText(Guid id) => Convert.ToBase64String(tokens[id]);

        public Task<ProductPage> ListAsync(Guid tenantId, ProductListFilter filter, CancellationToken cancellationToken)
        {
            TenantsSeen.Add(tenantId);
            var items = Products.Where(product => product.TenantId == tenantId).ToArray();
            return Task.FromResult(new ProductPage(items, items.Length));
        }

        public Task<Product?> FindAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken)
        {
            TenantsSeen.Add(tenantId);
            return Task.FromResult(Products.SingleOrDefault(product => product.Id == productId && product.TenantId == tenantId));
        }

        public Task<Product?> FindForUpdateAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken) =>
            FindAsync(tenantId, productId, cancellationToken);

        public Task<bool> ActiveCodeExistsAsync(Guid tenantId, string code, Guid? excludingProductId, CancellationToken cancellationToken)
        {
            TenantsSeen.Add(tenantId);
            return Task.FromResult(Products.Any(product => product.TenantId == tenantId && product.Id != excludingProductId &&
                string.Equals(product.Code, code, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<ProductSaveOutcome> AddAsync(Product product, CancellationToken cancellationToken)
        {
            Products.Add(product);
            tokens[product.Id] = BitConverter.GetBytes(++counter);
            return Task.FromResult(ProductSaveOutcome.Saved);
        }

        public Task<ProductSaveOutcome> SaveChangesAsync(Product product, byte[] originalRowVersion, CancellationToken cancellationToken)
        {
            LastOriginalRowVersion = originalRowVersion;
            if (!tokens[product.Id].SequenceEqual(originalRowVersion))
            {
                return Task.FromResult(ProductSaveOutcome.ConcurrencyConflict);
            }

            tokens[product.Id] = BitConverter.GetBytes(++counter);
            return Task.FromResult(ProductSaveOutcome.Saved);
        }
    }
}
