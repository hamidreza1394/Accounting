using Faranou.Vendor.Persistence.Domain;

namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Derived payment state of one sales order (D-108 §6). Nothing here is stored: the status is
/// computed at read time from the order total and the sum of Verified payments only. Recorded or
/// Rejected payments never reduce debt. A zero-amount order has nothing to collect.
/// </summary>
public static class VendorSalesOrderPaymentRules
{
    public const string StatusUnpaid = "Unpaid";
    public const string StatusPartiallyPaid = "PartiallyPaid";
    public const string StatusPaid = "Paid";

    public sealed record VendorSalesOrderPaymentStatus(
        string PaymentStatus,
        bool Overdue,
        decimal VerifiedPaidAmount,
        decimal RemainingAmount);

    public static VendorSalesOrderPaymentStatus Derive(
        decimal totalAmount,
        decimal verifiedPaidAmount,
        DateTimeOffset? dueAtUtc,
        DateTimeOffset nowUtc)
    {
        if (totalAmount < 0 || verifiedPaidAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAmount), "Order and payment amounts must not be negative.");
        }

        var remaining = totalAmount - verifiedPaidAmount;
        if (remaining < 0)
        {
            // Overpayments do not create debt; the excess is reported, never hidden.
            remaining = 0;
        }

        var paidAmount = Math.Min(verifiedPaidAmount, totalAmount);
        var status = remaining == 0
            ? StatusPaid
            : paidAmount > 0 ? StatusPartiallyPaid : StatusUnpaid;
        var overdue = remaining > 0 && dueAtUtc is { } due && due <= nowUtc;
        return new VendorSalesOrderPaymentStatus(status, overdue, paidAmount, remaining);
    }

    /// <summary>Sum of Verified payments only; Recorded and Rejected never count (D-108 §5).</summary>
    public static decimal SumVerifiedPayments(IEnumerable<VendorPaymentRecord> payments)
    {
        ArgumentNullException.ThrowIfNull(payments);
        var total = 0m;
        foreach (var payment in payments)
        {
            if (payment.Status == VendorPaymentRecord.StatusVerified)
            {
                total += payment.Amount;
            }
        }

        return total;
    }
}
