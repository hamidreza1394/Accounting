using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.ReferenceData;
using Faranou.Modules.Trade.Domain.Products;

namespace Faranou.Modules.Trade.Application.Products;

/// <summary>
/// Product use cases: validation and mapping. The tenant and user come only from the server-validated
/// <see cref="TradeAccessContext" /> passed by the caller, never from a request body. Validation messages
/// never echo the submitted value.
/// </summary>
public sealed class ProductService(IProductStore store, TimeProvider timeProvider)
{
    public const int MaxSearchLength = 100;
    public const int MaxPage = 100_000;
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;
    public const int RowVersionLength = 8;

    private const int CodeMax = 64;
    private const int NameMax = 200;
    private const int GroupMax = 64;
    private const int HsCodeMax = 16;

    private const string ForbiddenCharactersMessage = "مقدار شامل نویسه‌های کنترلی یا جهت‌دهی متن غیرمجاز است.";

    public async Task<ProductResult<ProductListResponse>> ListAsync(
        TradeAccessContext context,
        ProductListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var search = Optional(query.Search);
        if (search?.Length > MaxSearchLength)
        {
            errors["search"] = ["عبارت جست‌وجو بیش از حد طولانی است."];
        }

        var group = Optional(query.ProductGroupCode);
        if (group is not null && (HasForbiddenCharacters(group) || group.Length > GroupMax))
        {
            errors["productGroupCode"] = ["کد گروه کالا نامعتبر است."];
        }

        if (errors.Count > 0)
        {
            return new ProductResult<ProductListResponse>(ProductOutcome.ValidationFailed, Errors: errors);
        }

        var page = Math.Clamp(query.Page, 1, MaxPage);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var result = await store.ListAsync(
            context.TenantId,
            new ProductListFilter(search, group, query.IsActive, (page - 1) * pageSize, pageSize),
            cancellationToken);

        return new ProductResult<ProductListResponse>(
            ProductOutcome.Success,
            new ProductListResponse(result.Items.Select(ToListItem).ToArray(), page, pageSize, result.TotalCount));
    }

    public async Task<ProductResult<ProductDetail>> GetAsync(
        TradeAccessContext context,
        Guid productId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var product = await store.FindAsync(context.TenantId, productId, cancellationToken);
        return product is null
            ? new ProductResult<ProductDetail>(ProductOutcome.NotFound)
            : new ProductResult<ProductDetail>(ProductOutcome.Success, ToDetail(product));
    }

    public async Task<ProductResult<ProductDetail>> CreateAsync(
        TradeAccessContext context,
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var validated = Validate(request.Code, request.Name, request.ProductGroupCode, request.HsCode,
            request.BaseUnitCode, rowVersion: null, requireRowVersion: false, out var errors);
        if (validated is null)
        {
            return new ProductResult<ProductDetail>(ProductOutcome.ValidationFailed, Errors: errors);
        }

        if (await store.ActiveCodeExistsAsync(context.TenantId, validated.Code, null, cancellationToken))
        {
            return new ProductResult<ProductDetail>(ProductOutcome.DuplicateCode);
        }

        var product = new Product(
            context.TenantId,
            validated.Code,
            validated.Name,
            context.UserId,
            timeProvider.GetUtcNow().UtcDateTime,
            validated.ProductGroupCode,
            validated.HsCode,
            validated.BaseUnitCode);

        return ToResult(product, await store.AddAsync(product, cancellationToken));
    }

    public async Task<ProductResult<ProductDetail>> UpdateAsync(
        TradeAccessContext context,
        Guid productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var validated = Validate(request.Code, request.Name, request.ProductGroupCode, request.HsCode,
            request.BaseUnitCode, request.RowVersion, requireRowVersion: true, out var errors);
        if (validated is null)
        {
            return new ProductResult<ProductDetail>(ProductOutcome.ValidationFailed, Errors: errors);
        }

        var product = await store.FindForUpdateAsync(context.TenantId, productId, cancellationToken);
        if (product is null)
        {
            return new ProductResult<ProductDetail>(ProductOutcome.NotFound);
        }

        if (!string.Equals(product.Code, validated.Code, StringComparison.OrdinalIgnoreCase) &&
            await store.ActiveCodeExistsAsync(context.TenantId, validated.Code, product.Id, cancellationToken))
        {
            return new ProductResult<ProductDetail>(ProductOutcome.DuplicateCode);
        }

        // UpdatedAtUtc is stamped from the clock on every call, so even a no-op update modifies the row
        // and therefore compares the row version.
        product.UpdateDetails(
            validated.Code,
            validated.Name,
            validated.ProductGroupCode,
            validated.HsCode,
            validated.BaseUnitCode,
            context.UserId,
            timeProvider.GetUtcNow().UtcDateTime);

        return ToResult(product, await store.SaveChangesAsync(product, validated.RowVersion!, cancellationToken));
    }

    private static ProductResult<ProductDetail> ToResult(Product product, ProductSaveOutcome outcome) => outcome switch
    {
        ProductSaveOutcome.Saved => new ProductResult<ProductDetail>(ProductOutcome.Success, ToDetail(product)),
        ProductSaveOutcome.DuplicateCode => new ProductResult<ProductDetail>(ProductOutcome.DuplicateCode),
        _ => new ProductResult<ProductDetail>(ProductOutcome.ConcurrencyConflict),
    };

    private sealed record ValidatedProduct(
        string Code,
        string Name,
        string? ProductGroupCode,
        string? HsCode,
        string BaseUnitCode,
        byte[]? RowVersion);

    private static ValidatedProduct? Validate(
        string? code,
        string? name,
        string? productGroupCode,
        string? hsCode,
        string? baseUnitCode,
        string? rowVersion,
        bool requireRowVersion,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var normalizedCode = code?.Trim();
        if (string.IsNullOrEmpty(normalizedCode))
        {
            errors["code"] = ["کد الزامی است."];
        }
        else if (HasForbiddenCharacters(normalizedCode))
        {
            errors["code"] = [ForbiddenCharactersMessage];
        }
        else if (normalizedCode.Length > CodeMax)
        {
            errors["code"] = [$"کد نباید بیش از {CodeMax} نویسه باشد."];
        }

        var normalizedName = name?.Trim();
        if (string.IsNullOrEmpty(normalizedName))
        {
            errors["name"] = ["نام الزامی است."];
        }
        else if (HasForbiddenCharacters(normalizedName))
        {
            errors["name"] = [ForbiddenCharactersMessage];
        }
        else if (normalizedName.Length > NameMax)
        {
            errors["name"] = [$"نام نباید بیش از {NameMax} نویسه باشد."];
        }

        var normalizedGroup = Optional(productGroupCode);
        if (normalizedGroup is not null && HasForbiddenCharacters(normalizedGroup))
        {
            errors["productGroupCode"] = [ForbiddenCharactersMessage];
        }
        else if (normalizedGroup?.Length > GroupMax)
        {
            errors["productGroupCode"] = [$"کد گروه کالا نباید بیش از {GroupMax} نویسه باشد."];
        }

        // The HS code column is non-Unicode (varchar): anything outside printable ASCII would be silently
        // replaced by '?' on save, so it is rejected instead. The code stays text (formatting, leading zeroes).
        var normalizedHs = Optional(hsCode);
        if (normalizedHs is not null && (HasForbiddenCharacters(normalizedHs) || normalizedHs.Any(character => character is < ' ' or > '~')))
        {
            errors["hsCode"] = ["کد تعرفه فقط می‌تواند شامل نویسه‌های لاتین قابل‌چاپ باشد."];
        }
        else if (normalizedHs?.Length > HsCodeMax)
        {
            errors["hsCode"] = [$"کد تعرفه نباید بیش از {HsCodeMax} نویسه باشد."];
        }

        string? normalizedUnit = null;
        var unit = baseUnitCode?.Trim();
        if (string.IsNullOrEmpty(unit))
        {
            errors["baseUnitCode"] = ["واحد پایه الزامی است."];
        }
        else if (!TradeReferenceData.TryFindUnit(unit, out var found))
        {
            errors["baseUnitCode"] = ["واحد پایه نامعتبر است."];
        }
        else
        {
            normalizedUnit = found.Code.ToUpperInvariant();
        }

        byte[]? token = null;
        if (requireRowVersion && !TryParseRowVersion(rowVersion, out token))
        {
            errors["rowVersion"] = ["نسخه ردیف الزامی و باید معتبر باشد."];
        }

        return errors.Count > 0
            ? null
            : new ValidatedProduct(normalizedCode!, normalizedName!, normalizedGroup, normalizedHs, normalizedUnit!, token);
    }

    /// <summary>
    /// Control characters and Unicode bidi overrides/isolates/marks are rejected: they can disguise text in
    /// lists and exports. ZWNJ (U+200C), needed by Persian text, stays allowed. No code allowlist by design.
    /// </summary>
    private static bool HasForbiddenCharacters(string value) => value.Any(character =>
        char.IsControl(character) ||
        character is '\u200E' or '\u200F' or '\u061C' or '\u2028' or '\u2029' or '\uFEFF' ||
        character is (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069'));

    private static string? Optional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool TryParseRowVersion(string? value, out byte[]? token)
    {
        token = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var buffer = new byte[RowVersionLength + 4];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written != RowVersionLength)
        {
            return false;
        }

        token = buffer[..written];
        return true;
    }

    private static ProductListItem ToListItem(Product product) => new(
        product.Id,
        product.Code,
        product.Name,
        product.ProductGroupCode,
        product.HsCode,
        product.BaseUnitCode,
        product.IsActive);

    private static ProductDetail ToDetail(Product product) => new(
        product.Id,
        product.Code,
        product.Name,
        product.ProductGroupCode,
        product.HsCode,
        product.BaseUnitCode,
        product.IsActive,
        Convert.ToBase64String(product.RowVersion),
        DateTime.SpecifyKind(product.CreatedAtUtc, DateTimeKind.Utc),
        product.UpdatedAtUtc is null ? null : DateTime.SpecifyKind(product.UpdatedAtUtc.Value, DateTimeKind.Utc));
}
