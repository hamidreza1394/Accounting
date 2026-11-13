using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.Products;
using Faranou.Modules.Trade.Domain.Products;
using Xunit;

namespace Template.Tests;

public sealed class TradeProductServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly TradeAccessContext Context = new(UserId, TenantId, Guid.NewGuid(), true);

    private readonly FakeProductStore store = new();
    private readonly ProductService service;

    public TradeProductServiceTests() => service = new ProductService(store, new FixedTimeProvider());

    private static CreateProductRequest Create(
        string? code = "PR-1", string? name = "Steel", string? group = null, string? hs = null, string? unit = "TON") =>
        new(code, name, group, hs, unit);

    private static UpdateProductRequest Update(
        string rowVersion, string code = "PR-1", string name = "Steel", string? unit = "TON") =>
        new(code, name, null, null, unit, rowVersion);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RequiresCodeNameAndUnitAfterTrim(string blank)
    {
        AssertInvalid(await service.CreateAsync(Context, Create(code: blank), CancellationToken.None), "code");
        AssertInvalid(await service.CreateAsync(Context, Create(name: blank), CancellationToken.None), "name");
        AssertInvalid(await service.CreateAsync(Context, Create(unit: blank), CancellationToken.None), "baseUnitCode");
        AssertInvalid(await service.CreateAsync(Context, Create(unit: null), CancellationToken.None), "baseUnitCode");
        Assert.Empty(store.Products);
    }

    [Fact]
    public async Task Create_RejectsTooLongValuesWithoutEchoingThem()
    {
        var longCode = new string('c', 65);
        var longName = new string('n', 201);
        var longGroup = new string('g', 65);
        var longHs = new string('1', 17);

        AssertInvalid(await service.CreateAsync(Context, Create(code: longCode), CancellationToken.None), "code", longCode);
        AssertInvalid(await service.CreateAsync(Context, Create(name: longName), CancellationToken.None), "name", longName);
        AssertInvalid(await service.CreateAsync(Context, Create(group: longGroup), CancellationToken.None), "productGroupCode", longGroup);
        AssertInvalid(await service.CreateAsync(Context, Create(hs: longHs), CancellationToken.None), "hsCode", longHs);
        Assert.Empty(store.Products);
    }

    [Theory]
    [InlineData("a\u0000b")]
    [InlineData("a\nb")]
    [InlineData("a\tb")]
    [InlineData("a\u007Fb")]
    [InlineData("a\u202Eb")]
    [InlineData("a\u2066b")]
    [InlineData("a\u200Eb")]
    [InlineData("a\u200Fb")]
    [InlineData("a\u061Cb")]
    [InlineData("a\u2028b")]
    [InlineData("a\u2029b")]
    [InlineData("a\uFEFFb")]
    public async Task Create_RejectsControlAndBidiCharactersInEveryTextField(string bad)
    {
        AssertInvalid(await service.CreateAsync(Context, Create(code: bad), CancellationToken.None), "code", bad);
        AssertInvalid(await service.CreateAsync(Context, Create(name: bad), CancellationToken.None), "name", bad);
        AssertInvalid(await service.CreateAsync(Context, Create(group: bad), CancellationToken.None), "productGroupCode", bad);
        AssertInvalid(await service.CreateAsync(Context, Create(hs: bad), CancellationToken.None), "hsCode", bad);
        Assert.Empty(store.Products);
    }

    [Fact]
    public async Task Create_HsCodeStaysTextAndRejectsNonAsciiBecauseTheColumnIsVarchar()
    {
        var ok = await service.CreateAsync(Context, Create(hs: " 0072.10.1 "), CancellationToken.None);
        Assert.Equal(ProductOutcome.Success, ok.Outcome);
        Assert.Equal("0072.10.1", ok.Value!.HsCode);

        AssertInvalid(await service.CreateAsync(Context, Create(code: "X", hs: "۷۲۱۰"), CancellationToken.None), "hsCode");
        var boundary = await service.CreateAsync(Context, Create(code: "B", hs: new string('1', 16)), CancellationToken.None);
        Assert.Equal(ProductOutcome.Success, boundary.Outcome);
    }

    [Fact]
    public async Task Create_AcceptsPersianTextWithZwnjAndBoundaryLengths()
    {
        var persian = await service.CreateAsync(
            Context, Create(code: "ک-۱۰۲", name: "ورق می\u200Cخواهم گرم", group: "فولاد\u200Cها"), CancellationToken.None);
        var boundary = await service.CreateAsync(
            Context, Create(code: new string('c', 64), name: new string('n', 200), group: new string('g', 64)), CancellationToken.None);

        Assert.Equal(ProductOutcome.Success, persian.Outcome);
        Assert.Contains("\u200C", persian.Value!.Name, StringComparison.Ordinal);
        Assert.Equal(ProductOutcome.Success, boundary.Outcome);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("TONS")]
    [InlineData("1")]
    [InlineData("ت")]
    public async Task Create_RejectsUnknownUnit(string unit)
    {
        AssertInvalid(await service.CreateAsync(Context, Create(unit: unit), CancellationToken.None), "baseUnitCode", unit);
    }

    [Fact]
    public async Task Create_UnitIsCaseInsensitiveAndStoredUpper()
    {
        var result = await service.CreateAsync(Context, Create(unit: " kg "), CancellationToken.None);

        Assert.Equal(ProductOutcome.Success, result.Outcome);
        Assert.Equal("KG", result.Value!.BaseUnitCode);
        Assert.Equal("KG", store.Products.Single().BaseUnitCode);
    }

    [Fact]
    public async Task Create_UsesContextTenantAndUserAndReportsDuplicateCodeCaseInsensitively()
    {
        var first = await service.CreateAsync(Context, Create(code: "  D-1 "), CancellationToken.None);
        var second = await service.CreateAsync(Context, Create(code: "d-1"), CancellationToken.None);

        Assert.Equal(ProductOutcome.Success, first.Outcome);
        Assert.Equal("D-1", first.Value!.Code);
        Assert.True(first.Value.IsActive);
        Assert.Equal(TenantId, store.Products.Single().TenantId);
        Assert.Equal(UserId, store.Products.Single().CreatedByUserId);
        Assert.Equal(ProductOutcome.DuplicateCode, second.Outcome);
    }

    [Fact]
    public async Task Update_RequiresValidRowVersion()
    {
        var product = await Seed();

        foreach (var token in new string?[] { null, "", "   ", "not base64!", Convert.ToBase64String([1, 2, 3]) })
        {
            AssertInvalid(await service.UpdateAsync(Context, product.Id, Update(token!), CancellationToken.None), "rowVersion");
        }
    }

    [Fact]
    public async Task Update_ReturnsNotFoundForUnknownProductAndValidatesBeforeLookup()
    {
        var notFound = await service.UpdateAsync(Context, Guid.NewGuid(), Update(store.TokenText(Guid.NewGuid())), CancellationToken.None);
        var invalid = await service.UpdateAsync(Context, Guid.NewGuid(), Update(store.TokenText(Guid.NewGuid()), unit: "NOPE"), CancellationToken.None);

        Assert.Equal(ProductOutcome.NotFound, notFound.Outcome);
        AssertInvalid(invalid, "baseUnitCode");
    }

    [Fact]
    public async Task Update_ChangesFieldsKeepsActiveStateAndStaleTokenConflicts()
    {
        var product = await Seed();
        var current = store.TokenText(product.Id);

        var changed = await service.UpdateAsync(
            Context, product.Id, new UpdateProductRequest("PR-1", "Renamed", "GRP", "7210", "kg", current), CancellationToken.None);
        var stale = await service.UpdateAsync(Context, product.Id, Update(current), CancellationToken.None);

        Assert.Equal(ProductOutcome.Success, changed.Outcome);
        Assert.Equal(("Renamed", "GRP", "7210", "KG"), (changed.Value!.Name, changed.Value.ProductGroupCode, changed.Value.HsCode, changed.Value.BaseUnitCode));
        Assert.True(changed.Value.IsActive);
        Assert.NotNull(changed.Value.UpdatedAtUtc);
        Assert.Equal(Convert.FromBase64String(current), store.LastOriginalRowVersion);
        Assert.Equal(ProductOutcome.ConcurrencyConflict, stale.Outcome);
    }

    [Fact]
    public async Task Update_DuplicateCodeOfAnotherProductConflictsButOwnCodeAndCaseChangeAreAllowed()
    {
        await Seed(code: "A-1");
        var second = await Seed(code: "B-1");

        var clash = await service.UpdateAsync(Context, second.Id, Update(store.TokenText(second.Id), code: "a-1"), CancellationToken.None);
        var ownCase = await service.UpdateAsync(Context, second.Id, Update(store.TokenText(second.Id), code: "b-1"), CancellationToken.None);

        Assert.Equal(ProductOutcome.DuplicateCode, clash.Outcome);
        Assert.Equal(ProductOutcome.Success, ownCase.Outcome);
    }

    [Fact]
    public async Task Create_WhenStoreReportsDuplicateCodeAfterPreCheck_ReturnsDuplicateCode()
    {
        store.ForcedSaveOutcome = ProductSaveOutcome.DuplicateCode;

        var result = await service.CreateAsync(Context, Create(code: "RACE-1"), CancellationToken.None);

        Assert.Equal(ProductOutcome.DuplicateCode, result.Outcome);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Update_WhenStoreReportsDuplicateCodeAfterPreCheck_ReturnsDuplicateCode()
    {
        var product = await Seed();
        var token = store.TokenText(product.Id);
        store.ForcedSaveOutcome = ProductSaveOutcome.DuplicateCode;

        var result = await service.UpdateAsync(Context, product.Id, Update(token, code: "RACE-2"), CancellationToken.None);

        Assert.Equal(ProductOutcome.DuplicateCode, result.Outcome);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task List_ValidatesSearchAndGroupAndClampsPaging()
    {
        AssertInvalid(await service.ListAsync(Context, new ProductListQuery(new string('s', 101), null, null, 1, 20), CancellationToken.None), "search");
        AssertInvalid(await service.ListAsync(Context, new ProductListQuery(null, new string('g', 65), null, 1, 20), CancellationToken.None), "productGroupCode");
        AssertInvalid(await service.ListAsync(Context, new ProductListQuery(null, "a\u202Eb", null, 1, 20), CancellationToken.None), "productGroupCode");

        var ok = await service.ListAsync(Context, new ProductListQuery("  x  ", " G1 ", true, -5, 5000), CancellationToken.None);
        Assert.Equal(1, ok.Value!.Page);
        Assert.Equal(100, ok.Value.PageSize);
        Assert.Equal("x", store.LastFilter!.Search);
        Assert.Equal("G1", store.LastFilter.ProductGroupCode);
        Assert.True(store.LastFilter.IsActive);
        Assert.Equal(0, store.LastFilter.Skip);

        var small = await service.ListAsync(Context, new ProductListQuery(null, null, null, 3, 0), CancellationToken.None);
        Assert.Equal(1, small.Value!.PageSize);
        Assert.Equal(2, store.LastFilter!.Skip);

        var blank = await service.ListAsync(Context, new ProductListQuery("   ", "  ", null, 1, 20), CancellationToken.None);
        Assert.Null(store.LastFilter!.Search);
        Assert.Null(store.LastFilter.ProductGroupCode);
        Assert.Equal(ProductOutcome.Success, blank.Outcome);
    }

    [Fact]
    public async Task Get_ReturnsNotFoundForOtherTenantProduct()
    {
        var product = await Seed();
        var other = new TradeAccessContext(UserId, Guid.NewGuid(), Guid.NewGuid(), true);

        Assert.Equal(ProductOutcome.NotFound, (await service.GetAsync(other, product.Id, CancellationToken.None)).Outcome);
        Assert.Equal(ProductOutcome.Success, (await service.GetAsync(Context, product.Id, CancellationToken.None)).Outcome);
    }

    private async Task<ProductDetail> Seed(string code = "PR-1")
    {
        var result = await service.CreateAsync(Context, Create(code: code), CancellationToken.None);
        Assert.Equal(ProductOutcome.Success, result.Outcome);
        return result.Value!;
    }

    private static void AssertInvalid<T>(ProductResult<T> result, string key, string? sentValue = null)
    {
        Assert.Equal(ProductOutcome.ValidationFailed, result.Outcome);
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

    private sealed class FakeProductStore : IProductStore
    {
        private readonly Dictionary<Guid, byte[]> tokens = [];
        private long counter;

        public List<Product> Products { get; } = [];

        /// <summary>When set, add/save report this outcome (a unique-index race lost after the pre-check).</summary>
        public ProductSaveOutcome? ForcedSaveOutcome { get; set; }

        public byte[]? LastOriginalRowVersion { get; private set; }

        public ProductListFilter? LastFilter { get; private set; }

        public string TokenText(Guid id) => tokens.TryGetValue(id, out var token)
            ? Convert.ToBase64String(token)
            : Convert.ToBase64String(BitConverter.GetBytes(0L));

        public Task<ProductPage> ListAsync(Guid tenantId, ProductListFilter filter, CancellationToken cancellationToken)
        {
            LastFilter = filter;
            var items = Products.Where(product => product.TenantId == tenantId).ToArray();
            return Task.FromResult(new ProductPage(items, items.Length));
        }

        public Task<Product?> FindAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken) =>
            Task.FromResult(Products.SingleOrDefault(product => product.Id == productId && product.TenantId == tenantId));

        public Task<Product?> FindForUpdateAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken) =>
            FindAsync(tenantId, productId, cancellationToken);

        public Task<bool> ActiveCodeExistsAsync(Guid tenantId, string code, Guid? excludingProductId, CancellationToken cancellationToken) =>
            Task.FromResult(Products.Any(product => product.TenantId == tenantId && product.Id != excludingProductId &&
                string.Equals(product.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task<ProductSaveOutcome> AddAsync(Product product, CancellationToken cancellationToken)
        {
            if (ForcedSaveOutcome is { } forced)
            {
                return Task.FromResult(forced);
            }

            Products.Add(product);
            Bump(product.Id);
            return Task.FromResult(ProductSaveOutcome.Saved);
        }

        public Task<ProductSaveOutcome> SaveChangesAsync(Product product, byte[] originalRowVersion, CancellationToken cancellationToken)
        {
            LastOriginalRowVersion = originalRowVersion;
            if (ForcedSaveOutcome is { } forced)
            {
                return Task.FromResult(forced);
            }

            if (!tokens[product.Id].SequenceEqual(originalRowVersion))
            {
                return Task.FromResult(ProductSaveOutcome.ConcurrencyConflict);
            }

            Bump(product.Id);
            return Task.FromResult(ProductSaveOutcome.Saved);
        }

        private void Bump(Guid id) => tokens[id] = BitConverter.GetBytes(++counter);
    }
}
