using Faranou.Modules.Trade.Domain.Products;

namespace Faranou.Modules.Trade.Application.Products;

/// <summary>Validated list filter; every store query is additionally bound to one tenant.</summary>
public sealed record ProductListFilter(string? Search, string? ProductGroupCode, bool? IsActive, int Skip, int Take);

public sealed record ProductPage(IReadOnlyList<Product> Items, int TotalCount);

public enum ProductSaveOutcome
{
    Saved = 1,
    DuplicateCode = 2,
    ConcurrencyConflict = 3,
}

/// <summary>
/// Narrow persistence port of the Product use cases (not a generic repository). Every method takes
/// the tenant explicitly; soft-deleted products are never returned. Product identifiers are not
/// loaded or written (their table is not part of the S2 migration, D-081).
/// </summary>
public interface IProductStore
{
    Task<ProductPage> ListAsync(Guid tenantId, ProductListFilter filter, CancellationToken cancellationToken);

    /// <summary>Read-only lookup; null when missing, soft-deleted or of another tenant.</summary>
    Task<Product?> FindAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken);

    /// <summary>Tracked lookup for a following <see cref="SaveChangesAsync" />.</summary>
    Task<Product?> FindForUpdateAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken);

    Task<bool> ActiveCodeExistsAsync(Guid tenantId, string code, Guid? excludingProductId, CancellationToken cancellationToken);

    Task<ProductSaveOutcome> AddAsync(Product product, CancellationToken cancellationToken);

    /// <summary>Persists the changes of a product obtained by <see cref="FindForUpdateAsync" /> against the given row version.</summary>
    Task<ProductSaveOutcome> SaveChangesAsync(Product product, byte[] originalRowVersion, CancellationToken cancellationToken);
}
