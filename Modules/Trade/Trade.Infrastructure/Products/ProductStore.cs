using Faranou.Modules.Trade.Application.Products;
using Faranou.Modules.Trade.Domain.Products;
using Faranou.Modules.Trade.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Faranou.Modules.Trade.Infrastructure.Products;

/// <summary>
/// EF Core implementation of <see cref="IProductStore" />. No global query filter exists (D-013), so every
/// query states the tenant and the soft-delete predicate explicitly. Identifiers are never included.
/// </summary>
internal sealed partial class ProductStore(TradeDbContext database, ILogger<ProductStore> logger) : IProductStore
{
    private const string ActiveCodeIndex = "UX_Products_Tenant_Code_Active";

    public async Task<ProductPage> ListAsync(Guid tenantId, ProductListFilter filter, CancellationToken cancellationToken)
    {
        var query = database.Products.AsNoTracking()
            .Where(product => product.TenantId == tenantId && !product.IsDeleted);

        if (filter.Search is { } search)
        {
            // Contains is translated with escaped wildcards, so % and _ match literally.
            query = query.Where(product => product.Code.Contains(search) || product.Name.Contains(search));
        }

        if (filter.ProductGroupCode is { } group)
        {
            query = query.Where(product => product.ProductGroupCode == group);
        }

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(product => product.IsActive == isActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(product => product.Code).ThenBy(product => product.Id)
            .Skip(filter.Skip).Take(filter.Take)
            .ToListAsync(cancellationToken);

        return new ProductPage(items, totalCount);
    }

    public async Task<Product?> FindAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken) =>
        await database.Products.AsNoTracking().SingleOrDefaultAsync(
            product => product.Id == productId && product.TenantId == tenantId && !product.IsDeleted,
            cancellationToken);

    public async Task<Product?> FindForUpdateAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken) =>
        await database.Products.SingleOrDefaultAsync(
            product => product.Id == productId && product.TenantId == tenantId && !product.IsDeleted,
            cancellationToken);

    public Task<bool> ActiveCodeExistsAsync(
        Guid tenantId, string code, Guid? excludingProductId, CancellationToken cancellationToken) =>
        database.Products.AsNoTracking().AnyAsync(
            product => product.TenantId == tenantId && !product.IsDeleted && product.Code == code &&
                       (excludingProductId == null || product.Id != excludingProductId),
            cancellationToken);

    public async Task<ProductSaveOutcome> AddAsync(Product product, CancellationToken cancellationToken)
    {
        database.Products.Add(product);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return ProductSaveOutcome.Saved;
        }
        catch (DbUpdateException exception) when (IsUnique(exception, ActiveCodeIndex))
        {
            database.ChangeTracker.Clear();
            LogOutcome(logger, product.Id, "trade_product_code_duplicate");
            return ProductSaveOutcome.DuplicateCode;
        }
    }

    public async Task<ProductSaveOutcome> SaveChangesAsync(
        Product product, byte[] originalRowVersion, CancellationToken cancellationToken)
    {
        database.Entry(product).Property(item => item.RowVersion).OriginalValue = originalRowVersion;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return ProductSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            LogOutcome(logger, product.Id, "trade_concurrency_conflict");
            return ProductSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (IsUnique(exception, ActiveCodeIndex))
        {
            database.ChangeTracker.Clear();
            LogOutcome(logger, product.Id, "trade_product_code_duplicate");
            return ProductSaveOutcome.DuplicateCode;
        }
    }

    private static bool IsUnique(DbUpdateException exception, string indexName) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains(indexName, StringComparison.Ordinal);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Trade product {ProductId} write rejected: {Outcome}.")]
    private static partial void LogOutcome(ILogger logger, Guid productId, string outcome);
}
