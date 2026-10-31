using Faranou.Modules.Trade.Domain.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faranou.Modules.Trade.Infrastructure.Persistence.Configurations;

internal sealed class PartyBankAccountConfiguration : IEntityTypeConfiguration<PartyBankAccount>
{
    public void Configure(EntityTypeBuilder<PartyBankAccount> builder)
    {
        TradeEntityConfiguration.Configure(builder);
        builder.ToTable("PartyBankAccounts", "trade");

        builder.Property(account => account.BankName).HasMaxLength(160).IsRequired();
        builder.Property(account => account.AccountNumber).HasMaxLength(80).IsRequired();
        builder.Property(account => account.Iban).HasMaxLength(34).IsUnicode(false);
        builder.Property(account => account.AccountHolder).HasMaxLength(160).IsRequired();
        builder.Property(account => account.VerificationStatus)
            .HasConversion<string>().HasMaxLength(16).IsUnicode(false).IsRequired();
        builder.Property(account => account.VerifiedAtUtc).HasColumnType("datetime2");
        builder.Property(account => account.RejectionReason).HasMaxLength(500);

        builder.HasIndex(account => new { account.TenantId, account.Iban })
            .IsUnique()
            .HasDatabaseName("UX_PartyBankAccounts_Tenant_Iban_Active")
            .HasFilter("[IsDeleted] = 0 AND [Iban] IS NOT NULL");

        builder.HasIndex(account => new { account.TenantId, account.PartyId, account.AccountNumber })
            .IsUnique()
            .HasDatabaseName("UX_PartyBankAccounts_Tenant_Party_Account_Active")
            .HasFilter("[IsDeleted] = 0");
    }
}
