using Faranou.Vendor.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Atomically allocates a globally monotonic snapshot sequence using the Vendor SQL Server row.
/// Must be called inside the same transaction that persists the signed snapshot and approval.
/// </summary>
public sealed class VendorOfflineRevocationSequenceAllocator(VendorDbContext db)
{
    public async Task<long> AllocateNextAsync(CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Revocation sequence allocation requires the caller's Vendor transaction.");

        var allocated = await db.Database.SqlQueryRaw<long>(
            """
            UPDATE [dbo].[VendorOfflineRevocationSequences] WITH (UPDLOCK, ROWLOCK)
            SET [LastSequence] = [LastSequence] + 1
            OUTPUT INSERTED.[LastSequence] AS [Value]
            WHERE [Id] = 1 AND [LastSequence] < 9223372036854775807;
            """)
            .ToListAsync(cancellationToken);

        if (allocated.Count != 1 || allocated[0] <= 0)
            throw new InvalidOperationException("The Vendor offline revocation sequence counter is unavailable.");

        return allocated[0];
    }
}
