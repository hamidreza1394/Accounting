using Faranou.Modules.Trade.Domain.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faranou.Modules.Trade.Infrastructure.Persistence.Configurations;

internal sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        TradeEntityConfiguration.Configure(builder);
        builder.ToTable("Parties", "trade");

        builder.Property(party => party.Code).HasMaxLength(64).IsRequired();
        builder.Property(party => party.LegalName).HasMaxLength(200).IsRequired();
        builder.Property(party => party.DisplayName).HasMaxLength(200);
        builder.Property(party => party.LegalIdentifier).HasMaxLength(64);
        builder.Property(party => party.CountryCode).HasMaxLength(2).IsUnicode(false);
        builder.Property(party => party.IsActive).IsRequired();

        builder.HasIndex(party => new { party.TenantId, party.Code })
            .IsUnique()
            .HasDatabaseName("UX_Parties_Tenant_Code_Active")
            .HasFilter("[IsDeleted] = 0");

        builder.HasMany(party => party.Roles)
            .WithOne()
            .HasForeignKey(role => new { role.TenantId, role.PartyId })
            .HasPrincipalKey(party => new { party.TenantId, party.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(party => party.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(party => party.BankAccounts)
            .WithOne()
            .HasForeignKey(account => new { account.TenantId, account.PartyId })
            .HasPrincipalKey(party => new { party.TenantId, party.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(party => party.BankAccounts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
