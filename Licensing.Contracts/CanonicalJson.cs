using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Deterministic UTF-8 JSON for signing: stable property shape, no indentation.
/// </summary>
public static class CanonicalJson
{
	public static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public static string SerializePayload(LicensePayload payload)
	{
		ArgumentNullException.ThrowIfNull(payload);
		var dto = new PayloadDto
		{
			FormatVersion = payload.FormatVersion,
			LicenseId = payload.LicenseId,
			InstallationId = payload.InstallationId,
			ProductCode = payload.ProductCode,
			LicenseKind = payload.LicenseKind,
			IssuedAtUtc = payload.IssuedAtUtc.ToUniversalTime().ToString("O"),
			NotBeforeUtc = payload.NotBeforeUtc?.ToUniversalTime().ToString("O"),
			ExpiresAtUtc = payload.ExpiresAtUtc?.ToUniversalTime().ToString("O"),
			GraceDays = payload.GraceDays,
			Sequence = payload.Sequence,
			Limits = new LimitsDto
			{
				MaxCompanies = payload.Limits.MaxCompanies,
				Subsystems = payload.Limits.Subsystems.OrderBy(s => s, StringComparer.Ordinal).ToArray()
			}
		};
		return JsonSerializer.Serialize(dto, Options);
	}

	public static byte[] GetUtf8Bytes(string json) => Encoding.UTF8.GetBytes(json);

	private sealed class PayloadDto
	{
		public int FormatVersion { get; set; }
		public string LicenseId { get; set; } = "";
		public string InstallationId { get; set; } = "";
		public string ProductCode { get; set; } = "";
		public string LicenseKind { get; set; } = "";
		public string IssuedAtUtc { get; set; } = "";
		public string? NotBeforeUtc { get; set; }
		public string? ExpiresAtUtc { get; set; }
		public int GraceDays { get; set; }
		public int Sequence { get; set; }
		public LimitsDto Limits { get; set; } = new();
	}

	private sealed class LimitsDto
	{
		public int? MaxCompanies { get; set; }
		public string[] Subsystems { get; set; } = Array.Empty<string>();
	}
}
