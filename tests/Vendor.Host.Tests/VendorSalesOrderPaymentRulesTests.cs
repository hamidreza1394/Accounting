using Faranou.Vendor.Host.Authorization;
using Faranou.Vendor.Persistence.Domain;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

/// <summary>D-108 §5-6: read-time derivation from the order total and Verified payments only.</summary>
public sealed class VendorSalesOrderPaymentRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoVerifiedPaymentsIsUnpaidAndNotOverdueWithoutDueDate()
    {
        var status = VendorSalesOrderPaymentRules.Derive(1000m, 0m, null, Now);

        Assert.Equal(VendorSalesOrderPaymentRules.StatusUnpaid, status.PaymentStatus);
        Assert.False(status.Overdue);
        Assert.Equal(0m, status.VerifiedPaidAmount);
        Assert.Equal(1000m, status.RemainingAmount);
    }

    [Fact]
    public void PartialVerifiedPaymentIsPartiallyPaid()
    {
        var status = VendorSalesOrderPaymentRules.Derive(1000m, 400m, null, Now);

        Assert.Equal(VendorSalesOrderPaymentRules.StatusPartiallyPaid, status.PaymentStatus);
        Assert.Equal(400m, status.VerifiedPaidAmount);
        Assert.Equal(600m, status.RemainingAmount);
    }

    [Fact]
    public void FullyVerifiedPaymentIsPaid()
    {
        var status = VendorSalesOrderPaymentRules.Derive(1000m, 1000m, null, Now);

        Assert.Equal(VendorSalesOrderPaymentRules.StatusPaid, status.PaymentStatus);
        Assert.False(status.Overdue);
        Assert.Equal(0m, status.RemainingAmount);
    }

    [Fact]
    public void OverpaymentNeverCreatesDebt()
    {
        var status = VendorSalesOrderPaymentRules.Derive(1000m, 1200m, null, Now);

        Assert.Equal(VendorSalesOrderPaymentRules.StatusPaid, status.PaymentStatus);
        Assert.Equal(1000m, status.VerifiedPaidAmount);
        Assert.Equal(0m, status.RemainingAmount);
    }

    [Fact]
    public void PastDueDateWithRemainingDebtIsOverdue()
    {
        var due = Now.AddDays(-2);
        var status = VendorSalesOrderPaymentRules.Derive(1000m, 400m, due, Now);

        Assert.Equal(VendorSalesOrderPaymentRules.StatusPartiallyPaid, status.PaymentStatus);
        Assert.True(status.Overdue);
    }

    [Fact]
    public void PaidOrderIsNeverOverdueEvenWithPastDueDate()
    {
        var due = Now.AddDays(-2);
        var status = VendorSalesOrderPaymentRules.Derive(1000m, 1000m, due, Now);

        Assert.Equal(VendorSalesOrderPaymentRules.StatusPaid, status.PaymentStatus);
        Assert.False(status.Overdue);
    }

    [Fact]
    public void FutureDueDateIsNotOverdue()
    {
        var due = Now.AddDays(10);
        var status = VendorSalesOrderPaymentRules.Derive(1000m, 0m, due, Now);

        Assert.False(status.Overdue);
    }

    [Fact]
    public void DueDateAtNowCountsAsOverdue()
    {
        var status = VendorSalesOrderPaymentRules.Derive(1000m, 0m, Now, Now);

        Assert.True(status.Overdue);
    }

    [Fact]
    public void ZeroAmountOrderHasNothingToCollect()
    {
        var status = VendorSalesOrderPaymentRules.Derive(0m, 0m, Now.AddDays(-1), Now);

        Assert.Equal(VendorSalesOrderPaymentRules.StatusPaid, status.PaymentStatus);
        Assert.False(status.Overdue);
    }

    [Fact]
    public void OnlyVerifiedPaymentsReduceTheDebt()
    {
        var payments = new[]
        {
            new VendorPaymentRecord { Amount = 250m, Status = VendorPaymentRecord.StatusVerified },
            new VendorPaymentRecord { Amount = 300m, Status = VendorPaymentRecord.StatusRecorded },
            new VendorPaymentRecord { Amount = 400m, Status = VendorPaymentRecord.StatusRejected },
        };

        var verified = VendorSalesOrderPaymentRules.SumVerifiedPayments(payments);
        var status = VendorSalesOrderPaymentRules.Derive(1000m, verified, null, Now);

        Assert.Equal(250m, verified);
        Assert.Equal(VendorSalesOrderPaymentRules.StatusPartiallyPaid, status.PaymentStatus);
        Assert.Equal(750m, status.RemainingAmount);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void NegativeAmountsAreRejected(decimal total, decimal paid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VendorSalesOrderPaymentRules.Derive(total, paid, null, Now));
    }
}
