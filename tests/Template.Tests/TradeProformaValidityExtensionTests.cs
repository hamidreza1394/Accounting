using System.Reflection;
using Faranou.Modules.Trade.Application.Proformas;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The standalone history event for extending only a Proforma validity date (D-116 item 2). Pure and schema-free:
/// where the validity date is stored (A-1) and whether it may be shortened (A-2) are undecided and not modelled here.
/// </summary>
public sealed class TradeProformaValidityExtensionTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Proforma = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateOnly Previous = new(2027, 1, 31);
    private static readonly DateOnly Extended = new(2027, 3, 15);
    private static readonly DateTime Now = new(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc);

    private static bool Create(
        out ProformaValidityExtendedEvent? ev,
        out ProformaValidityExtensionIssue issue,
        Guid? tenant = null,
        Guid? company = null,
        Guid? proforma = null,
        DateOnly? previous = null,
        DateOnly? extended = null,
        string? reason = "customer request",
        Guid? actor = null,
        DateTime? recordedAtUtc = null) =>
        ProformaValidityExtendedEvent.TryCreate(
            tenant ?? Tenant,
            company ?? Company,
            proforma ?? Proforma,
            previous ?? Previous,
            extended ?? Extended,
            reason,
            actor ?? Actor,
            recordedAtUtc ?? Now,
            out ev,
            out issue);

    [Fact]
    public void ALaterDate_RecordsEveryAcceptedFieldWithExplicitTenantAndCompany()
    {
        Assert.True(Create(out var ev, out var issue));

        Assert.Equal(ProformaValidityExtensionIssue.None, issue);
        Assert.NotNull(ev);
        Assert.Equal(Tenant, ev.TenantId);
        Assert.Equal(Company, ev.CompanyId);
        Assert.NotEqual(ev.TenantId, ev.CompanyId);
        Assert.Equal(Proforma, ev.ProformaId);
        Assert.Equal(Previous, ev.PreviousValidUntil);
        Assert.Equal(Extended, ev.NewValidUntil);
        Assert.Equal("customer request", ev.Reason);
        Assert.Equal(Actor, ev.ActorUserId);
        Assert.Equal(Now, ev.RecordedAtUtc);
    }

    [Fact]
    public void TheSameCompanyAndTenantIdValue_IsNotRequiredOrAssumed()
    {
        // The legacy "TenantId == Company.Id" equality is a deviation, not a contract (D-087, D-116).
        var same = Guid.NewGuid();
        Assert.True(Create(out var ev, out _, tenant: same, company: same));
        Assert.Equal(same, ev!.TenantId);
        Assert.Equal(same, ev.CompanyId);
    }

    [Fact]
    public void AnEmptyTenant_IsRefusedAndNoEventExists() =>
        AssertRefused(Create(out var ev, out var issue, tenant: Guid.Empty), ev, issue, ProformaValidityExtensionIssue.TenantMissing);

    [Fact]
    public void AnEmptyCompany_IsRefusedAndNoEventExists() =>
        AssertRefused(Create(out var ev, out var issue, company: Guid.Empty), ev, issue, ProformaValidityExtensionIssue.CompanyMissing);

    [Fact]
    public void AnEmptyProforma_IsRefusedAndNoEventExists() =>
        AssertRefused(Create(out var ev, out var issue, proforma: Guid.Empty), ev, issue, ProformaValidityExtensionIssue.ProformaMissing);

    [Fact]
    public void AnEmptyActor_IsRefusedAndNoEventExists() =>
        AssertRefused(Create(out var ev, out var issue, actor: Guid.Empty), ev, issue, ProformaValidityExtensionIssue.ActorMissing);

    private static void AssertRefused(bool created, ProformaValidityExtendedEvent? ev, ProformaValidityExtensionIssue issue, ProformaValidityExtensionIssue expected)
    {
        Assert.False(created);
        Assert.Null(ev);
        Assert.Equal(expected, issue);
    }

    [Fact]
    public void TheSameDate_IsNotAnExtension()
    {
        Assert.False(Create(out var ev, out var issue, extended: Previous));
        Assert.Null(ev);
        Assert.Equal(ProformaValidityExtensionIssue.NotAnExtension, issue);
    }

    [Fact]
    public void AnEarlierDate_IsNotAnExtension_AndIsLeftUndecided()
    {
        // Shortening is owner decision A-2: it is refused here only because it is not an extension, never recorded as one.
        Assert.False(Create(out var ev, out var issue, extended: Previous.AddDays(-1)));
        Assert.Null(ev);
        Assert.Equal(ProformaValidityExtensionIssue.NotAnExtension, issue);
    }

    [Fact]
    public void AnExtensionByOneDay_IsAccepted()
    {
        Assert.True(Create(out var ev, out _, extended: Previous.AddDays(1)));
        Assert.Equal(Previous.AddDays(1), ev!.NewValidUntil);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void ARecordedTimeThatIsNotUtc_IsRefused(DateTimeKind kind)
    {
        var time = DateTime.SpecifyKind(Now, kind);

        Assert.False(Create(out var ev, out var issue, recordedAtUtc: time));
        Assert.Null(ev);
        Assert.Equal(ProformaValidityExtensionIssue.RecordedAtNotUtc, issue);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   \t ", null)]
    [InlineData("  buyer asked for time  ", "buyer asked for time")]
    public void TheReasonIsOptionalAndTrimmed(string? input, string? expected)
    {
        Assert.True(Create(out var ev, out _, reason: input));
        Assert.Equal(expected, ev!.Reason);
    }

    [Fact]
    public void AnEventCanOnlyComeFromTheValidatedFactory_AndIsImmutable()
    {
        var type = typeof(ProformaValidityExtendedEvent);

        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.Name != "EqualityContract"),
            property => Assert.True(
                property.SetMethod is null,
                $"{property.Name} must be get-only so a recorded event can not be edited."));
    }
}
