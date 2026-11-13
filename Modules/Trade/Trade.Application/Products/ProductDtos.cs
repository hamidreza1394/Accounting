namespace Faranou.Modules.Trade.Application.Products;

/// <summary>Product list row.</summary>
public sealed record ProductListItem(
    Guid Id,
    string Code,
    string Name,
    string? ProductGroupCode,
    string? HsCode,
    string BaseUnitCode,
    bool IsActive);

public sealed record ProductListResponse(
    IReadOnlyList<ProductListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Product detail: the list fields plus the row version and audit timestamps (no user ids).</summary>
public sealed record ProductDetail(
    Guid Id,
    string Code,
    string Name,
    string? ProductGroupCode,
    string? HsCode,
    string BaseUnitCode,
    bool IsActive,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Create request. Unknown fields (for example a tenant id or isActive) are ignored by deserialization.</summary>
public sealed record CreateProductRequest(
    string? Code,
    string? Name,
    string? ProductGroupCode,
    string? HsCode,
    string? BaseUnitCode);

/// <summary>Update request; the row version is mandatory. IsActive is intentionally not editable (D-083).</summary>
public sealed record UpdateProductRequest(
    string? Code,
    string? Name,
    string? ProductGroupCode,
    string? HsCode,
    string? BaseUnitCode,
    string? RowVersion);

/// <summary>Raw list query as received from the client; validated by <see cref="ProductService" />.</summary>
public sealed record ProductListQuery(
    string? Search,
    string? ProductGroupCode,
    bool? IsActive,
    int Page,
    int PageSize);

public enum ProductOutcome
{
    Success = 1,
    ValidationFailed = 2,
    NotFound = 3,
    DuplicateCode = 4,
    ConcurrencyConflict = 5,
}

public sealed record ProductResult<T>(
    ProductOutcome Outcome,
    T? Value = default,
    IReadOnlyDictionary<string, string[]>? Errors = null);
