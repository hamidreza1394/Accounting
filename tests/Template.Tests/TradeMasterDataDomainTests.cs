using Faranou.Modules.Trade.Domain.Parties;
using Faranou.Modules.Trade.Domain.Products;
using Xunit;

namespace Template.Tests;

public sealed class TradeMasterDataDomainTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Party_NormalizesCompanyScopedIdentity_AndSupportsMultipleRoles()
    {
        var party = new Party(TenantId, " SUP-001 ", " Supplier Legal Name ", UserId, Now,
            legalIdentifier: " 12345 ", countryCode: "tr", displayName: "Supplier");

        party.AddRole(PartyRole.Seller, UserId, Now);
        party.AddRole(PartyRole.Carrier, UserId, Now);

        Assert.Equal(TenantId, party.TenantId);
        Assert.Equal("SUP-001", party.Code);
        Assert.Equal("Supplier Legal Name", party.LegalName);
        Assert.Equal("TR", party.CountryCode);
        Assert.Equal(2, party.Roles.Count);
        Assert.Throws<InvalidOperationException>(() => party.AddRole(PartyRole.Seller, UserId, Now));
    }

    [Fact]
    public void PartyBankAccount_IsNotPaymentEligibleUntilExplicitlyVerified()
    {
        var party = new Party(TenantId, "BANK-01", "Foreign Bank", UserId, Now);
        var account = party.AddBankAccount(
            "Example Bank", " 000123 ", " tr 1200 0000 0000 0000 0000 0000 01 ",
            "Account Holder", UserId, Now);

        Assert.False(account.IsPaymentEligible);
        Assert.Equal(BankAccountVerificationStatus.Pending, account.VerificationStatus);
        Assert.Equal("TR12000000000000000000000001", account.Iban);

        account.Verify(UserId, Now.AddMinutes(1));

        Assert.True(account.IsPaymentEligible);
        Assert.Equal(UserId, account.VerifiedByUserId);
        Assert.Equal(Now.AddMinutes(1), account.VerifiedAtUtc);

        account.ReplaceDetails("Updated Bank", "000456", null, "New Holder", UserId, Now.AddMinutes(2));
        Assert.False(account.IsPaymentEligible);
        Assert.Equal(BankAccountVerificationStatus.Pending, account.VerificationStatus);
        Assert.Null(account.VerifiedByUserId);
        Assert.Null(account.VerifiedAtUtc);
    }

    [Fact]
    public void RejectedBankAccountRequiresReason_AndCannotBeUsedForPayment()
    {
        var party = new Party(TenantId, "BANK-02", "Another Bank", UserId, Now);
        var account = party.AddBankAccount("Bank", "123", null, "Holder", UserId, Now);

        Assert.Throws<ArgumentException>(() => account.Reject(UserId, Now, " "));
        account.Reject(UserId, Now, "Account owner did not match.");

        Assert.False(account.IsPaymentEligible);
        Assert.Equal(BankAccountVerificationStatus.Rejected, account.VerificationStatus);
        Assert.Equal("Account owner did not match.", account.RejectionReason);
    }

    [Fact]
    public void RemovingPartyRoleWithInvalidAuditActorDoesNotPartiallyDeleteChild()
    {
        var party = new Party(TenantId, "SUP-ATOMIC", "Atomic Supplier", UserId, Now);
        party.AddRole(PartyRole.Seller, UserId, Now);

        var role = Assert.Single(party.Roles);
        Assert.Throws<ArgumentException>(() => party.RemoveRole(PartyRole.Seller, Guid.Empty, Now.AddMinutes(1)));

        Assert.False(role.IsDeleted);
        Assert.Null(party.UpdatedAtUtc);
    }

    [Fact]
    public void RemovingProductIdentifierWithNonUtcTimestampDoesNotPartiallyDeleteChild()
    {
        var product = new Product(TenantId, "ATOMIC-01", "Atomic Product", UserId, Now);
        var identifier = product.AddIdentifier(
            null, "SEED", "US", "ATOMIC", "00012345678905", null,
            new DateOnly(2027, 1, 1), false, UserId, Now);

        Assert.Throws<ArgumentException>(() => product.RemoveIdentifier(
            identifier.Id, UserId, DateTime.SpecifyKind(Now.AddMinutes(1), DateTimeKind.Local)));

        Assert.False(identifier.IsDeleted);
        Assert.Null(product.UpdatedAtUtc);
    }

    [Fact]
    public void Product_PreservesHsAndIdentifiersAsText_AndKeepsGtinSeparateFromIrc()
    {
        var product = new Product(TenantId, "RICE-1121", "Parboiled Rice 1121", UserId, Now,
            productGroupCode: "GRAIN", hsCode: "010059010", baseUnitCode: "ton");

        var identifier = product.AddIdentifier(
            supplierPartyId: null,
            registrationSourceCode: "INDIA",
            originCountryCode: "in",
            commodityTypeCode: "PARBOILED",
            gtin: "00012345678905",
            irc: "0012345678901234",
            validUntil: new DateOnly(2027, 12, 31),
            isLinked: true,
            createdByUserId: UserId,
            createdAtUtc: Now);

        Assert.Equal("010059010", product.HsCode);
        Assert.Equal("TON", product.BaseUnitCode);
        Assert.Equal("00012345678905", identifier.Gtin);
        Assert.Equal("0012345678901234", identifier.Irc);
        Assert.NotEqual(identifier.Gtin, identifier.Irc);
        Assert.Equal("IN", identifier.OriginCountryCode);
    }

    [Fact]
    public void ProductIdentifier_RequiresAtLeastOneIndependentIdentifier()
    {
        var product = new Product(TenantId, "CORN-01", "Corn", UserId, Now);

        Assert.Throws<ArgumentException>(() => product.AddIdentifier(
            null, "SEED", "US", "CORN", null, null, new DateOnly(2027, 1, 1), false, UserId, Now));
    }

    [Fact]
    public void ProductIdentifier_RejectsMissingValidityDate()
    {
        var product = new Product(TenantId, "WHEAT-01", "Wheat", UserId, Now);

        Assert.Throws<ArgumentException>(() => product.AddIdentifier(
            null, "SEED", "US", "WHEAT", "00012345678905", null, default, false, UserId, Now));
    }

    [Fact]
    public void TradeEntity_SoftDeleteIsAuditedAndPreventsFurtherUpdates()
    {
        var product = new Product(TenantId, "SOY-01", "Soybean Meal", UserId, Now);

        product.SoftDelete(UserId, Now.AddMinutes(5));

        Assert.True(product.IsDeleted);
        Assert.Equal(UserId, product.DeletedByUserId);
        Assert.Equal(Now.AddMinutes(5), product.DeletedAtUtc);
        Assert.Throws<InvalidOperationException>(() => product.SetActive(false, UserId, Now.AddMinutes(6)));
    }

    [Fact]
    public void TradeAuditTimestampsMustBeUtc()
    {
        Assert.Throws<ArgumentException>(() =>
            new Party(TenantId, "BAD-DATE", "Invalid Timestamp", UserId, DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }
}
