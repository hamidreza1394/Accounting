using Faranou.Modules.Trade.Domain.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Faranou.Modules.Trade.Infrastructure.Persistence.Configurations;

internal sealed class PartyRoleAssignmentConfiguration : IEntityTypeConfiguration<PartyRoleAssignment>
{
    public void Configure(EntityTypeBuilder<PartyRoleAssignment> builder)
    {
        TradeEntityConfiguration.Configure(builder);
        builder.ToTable("PartyRoleAssignments", "trade");
        builder.Property(role => role.Role).HasConversion<string>().HasMaxLength(32).IsUnicode(false).IsRequired();

        builder.HasIndex(role => new { role.TenantId, role.PartyId, role.Role })
            .IsUnique()
            .HasDatabaseName("UX_PartyRoleAssignments_Tenant_Party_Role_Active")
            .HasFilter("[IsDeleted] = 0");
    }
}
