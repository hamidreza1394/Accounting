namespace Faranou.Vendor.Persistence.Domain;

/// <summary>Customer record owned by the Vendor control plane, separate from customer accounting databases.</summary>
public sealed class VendorCustomer
{
    public Guid Id { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<VendorInstallation> Installations { get; } = new List<VendorInstallation>();
    public ICollection<VendorContract> Contracts { get; } = new List<VendorContract>();
    public ICollection<VendorLicense> Licenses { get; } = new List<VendorLicense>();
}

/// <summary>Metadata for an installation; it never stores or opens the customer's database connection.</summary>
public sealed class VendorInstallation
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public VendorCustomer Customer { get; set; } = null!;
    public string InstallationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Environment { get; set; } = "Production";
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastSeenAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<VendorLicense> Licenses { get; } = new List<VendorLicense>();
}

/// <summary>Commercial contract metadata; it does not imply payment or legal approval.</summary>
public sealed class VendorContract
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public VendorCustomer Customer { get; set; } = null!;
    public string ContractNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<VendorLicense> Licenses { get; } = new List<VendorLicense>();
}

/// <summary>License entitlement metadata only; cryptographic license issuance is a separate phase.</summary>
public sealed class VendorLicense
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public VendorCustomer Customer { get; set; } = null!;
    public Guid? ContractId { get; set; }
    public VendorContract? Contract { get; set; }
    public Guid? InstallationId { get; set; }
    public VendorInstallation? Installation { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string LicenseKind { get; set; } = "term";
    public int? MaxCompanies { get; set; }
    public string SubsystemsJson { get; set; } = "[]";
    public int GraceDays { get; set; } = 30;
    public int LastIssuedSequence { get; set; }
    public DateTimeOffset ValidFromUtc { get; set; }
    public DateTimeOffset? ValidUntilUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
