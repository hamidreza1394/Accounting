namespace Domain;

/// <summary>
/// The personnel photo of a <see cref="CompanyEmployee"/> (at most one per employee). The bytes
/// live in this separate, company-scoped row — never in a public/wwwroot path and never on the
/// employee row itself — so listing employees does not load them. The content type is derived
/// from the file signature on the server; the user's file name is not stored.
/// </summary>
public sealed class CompanyEmployeePhoto : Seedwork.Entity
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }

    /// <summary>One of image/jpeg, image/png, image/webp (detected from the content).</summary>
    public string ContentType { get; set; } = string.Empty;
    public int ContentLength { get; set; }
    public byte[] Content { get; set; } = [];

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid UpdatedByUserId { get; set; }
}
