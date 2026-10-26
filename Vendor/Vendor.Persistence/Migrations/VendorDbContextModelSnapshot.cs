using Faranou.Vendor.Persistence.Auditing;
using Faranou.Vendor.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Faranou.Vendor.Persistence.Migrations;

[DbContext(typeof(VendorDbContext))]
public sealed class VendorDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("vendor");
        modelBuilder.HasAnnotation("ProductVersion", "10.0.13");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 128);

        modelBuilder.Entity<VendorAdminUser>(entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd();
            entity.Property<int>("AccessFailedCount");
            entity.Property<string>("ConcurrencyStamp").IsConcurrencyToken().HasColumnType("nvarchar(max)");
            entity.Property<DateTimeOffset?>("DisabledAtUtc");
            entity.Property<string>("DisplayName").IsRequired().HasMaxLength(160);
            entity.Property<string>("Email").HasMaxLength(256);
            entity.Property<bool>("EmailConfirmed");
            entity.Property<bool>("IsEnabled").HasDefaultValue(true);
            entity.Property<bool>("LockoutEnabled");
            entity.Property<DateTimeOffset?>("LockoutEnd").HasColumnType("datetimeoffset");
            entity.Property<string>("NormalizedEmail").HasMaxLength(256);
            entity.Property<string>("NormalizedUserName").HasMaxLength(256);
            entity.Property<string>("PasswordHash").HasColumnType("nvarchar(max)");
            entity.Property<string>("PhoneNumber").HasColumnType("nvarchar(max)");
            entity.Property<bool>("PhoneNumberConfirmed");
            entity.Property<string>("SecurityStamp").HasColumnType("nvarchar(max)");
            entity.Property<bool>("TwoFactorEnabled");
            entity.Property<string>("UserName").HasMaxLength(256);
            entity.Property<DateTimeOffset>("CreatedAtUtc");
            entity.HasKey("Id");
            entity.HasIndex("NormalizedEmail").HasDatabaseName("EmailIndex");
            entity.HasIndex("NormalizedUserName").IsUnique().HasDatabaseName("UserNameIndex").HasFilter("[NormalizedUserName] IS NOT NULL");
            entity.ToTable("AdminUsers", "vendor");
        });

        modelBuilder.Entity<IdentityRole<Guid>>(entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd();
            entity.Property<string>("ConcurrencyStamp").HasColumnType("nvarchar(max)");
            entity.Property<string>("Name").HasMaxLength(256);
            entity.Property<string>("NormalizedName").HasMaxLength(256);
            entity.HasKey("Id");
            entity.HasIndex("NormalizedName").IsUnique().HasDatabaseName("RoleNameIndex").HasFilter("[NormalizedName] IS NOT NULL");
            entity.ToTable("AdminRoles", "vendor");
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(entity =>
        {
            entity.Property<int>("Id").ValueGeneratedOnAdd();
            entity.Property<string>("ClaimType").HasColumnType("nvarchar(max)");
            entity.Property<string>("ClaimValue").HasColumnType("nvarchar(max)");
            entity.Property<Guid>("UserId");
            entity.HasKey("Id");
            entity.HasIndex("UserId");
            entity.ToTable("AdminUserClaims", "vendor");
        });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>(entity =>
        {
            entity.Property<int>("Id").ValueGeneratedOnAdd();
            entity.Property<string>("ClaimType").HasColumnType("nvarchar(max)");
            entity.Property<string>("ClaimValue").HasColumnType("nvarchar(max)");
            entity.Property<Guid>("RoleId");
            entity.HasKey("Id");
            entity.HasIndex("RoleId");
            entity.ToTable("AdminRoleClaims", "vendor");
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.Property<string>("LoginProvider").HasMaxLength(450);
            entity.Property<string>("ProviderKey").HasMaxLength(450);
            entity.Property<string>("ProviderDisplayName").HasColumnType("nvarchar(max)");
            entity.Property<Guid>("UserId");
            entity.HasKey("LoginProvider", "ProviderKey");
            entity.HasIndex("UserId");
            entity.ToTable("AdminUserLogins", "vendor");
        });

        modelBuilder.Entity<IdentityUserRole<Guid>>(entity =>
        {
            entity.Property<Guid>("UserId");
            entity.Property<Guid>("RoleId");
            entity.HasKey("UserId", "RoleId");
            entity.HasIndex("RoleId");
            entity.ToTable("AdminUserRoles", "vendor");
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.Property<Guid>("UserId");
            entity.Property<string>("LoginProvider").HasMaxLength(450);
            entity.Property<string>("Name").HasMaxLength(450);
            entity.Property<string>("Value").HasColumnType("nvarchar(max)");
            entity.HasKey("UserId", "LoginProvider", "Name");
            entity.ToTable("AdminUserTokens", "vendor");
        });

        modelBuilder.Entity<VendorAuditEvent>(entity =>
        {
            entity.Property<long>("Id").ValueGeneratedOnAdd();
            entity.Property<string>("Action").IsRequired().HasMaxLength(120);
            entity.Property<Guid?>("ActorUserId");
            entity.Property<string>("CorrelationId").IsRequired().HasMaxLength(128);
            entity.Property<string>("DetailsJson").IsRequired().HasColumnType("nvarchar(max)");
            entity.Property<byte[]>("EventHash").IsRequired().HasColumnType("varbinary(32)");
            entity.Property<DateTimeOffset>("OccurredAtUtc");
            entity.Property<string>("Outcome").IsRequired().HasMaxLength(32);
            entity.Property<byte[]>("PreviousHash").IsRequired().HasColumnType("varbinary(32)");
            entity.Property<string>("TargetId").HasMaxLength(200);
            entity.Property<string>("TargetType").IsRequired().HasMaxLength(120);
            entity.HasKey("Id");
            entity.HasIndex("CorrelationId");
            entity.HasIndex("OccurredAtUtc");
            entity.ToTable("AuditEvents", "vendor");
        });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>()
            .HasOne<IdentityRole<Guid>>()
            .WithMany()
            .HasForeignKey("RoleId")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<IdentityUserClaim<Guid>>()
            .HasOne<VendorAdminUser>()
            .WithMany()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<IdentityUserLogin<Guid>>()
            .HasOne<VendorAdminUser>()
            .WithMany()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<IdentityUserRole<Guid>>()
            .HasOne<IdentityRole<Guid>>()
            .WithMany()
            .HasForeignKey("RoleId")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<IdentityUserRole<Guid>>()
            .HasOne<VendorAdminUser>()
            .WithMany()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        modelBuilder.Entity<IdentityUserToken<Guid>>()
            .HasOne<VendorAdminUser>()
            .WithMany()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
