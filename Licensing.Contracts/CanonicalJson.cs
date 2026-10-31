using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Project-specific deterministic UTF-8 JSON for signing (NOT RFC 8785).
/// Contracts v1/v2: fixed DTO property order, camelCase, nulls omitted, subsystems sorted Ordinal; v2 adds the signed installation-key fingerprint,
/// DateTimeOffset as round-trip "O" UTC. Options are private and not mutable from outside.
/// </summary>
public static class CanonicalJson
{
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private static readonly JsonSerializerOptions ParseOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = false,
		AllowTrailingCommas = false,
		ReadCommentHandling = JsonCommentHandling.Disallow,
		MaxDepth = 8
	};

	public static string SerializePayload(LicensePayload payload)
	{
		ArgumentNullException.ThrowIfNull(payload);
		var subsystems = payload.Limits.Subsystems ?? Array.Empty<string>();
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
				Subsystems = subsystems.OrderBy(s => s, StringComparer.Ordinal).ToArray()
			}
		};
		return JsonSerializer.Serialize(dto, Options);
	}

	public static byte[] GetUtf8Bytes(string json) => Encoding.UTF8.GetBytes(json);

	public static bool TryParsePayload(string payloadJson, out LicensePayload? payload, out string? errorCode)
	{
		payload = null;
		errorCode = null;
		if (string.IsNullOrEmpty(payloadJson))
		{
			errorCode = "payload_empty";
			return false;
		}
		if (payloadJson.Length > LicenseInputLimits.MaxPayloadJsonChars)
		{
			errorCode = "payload_too_large";
			return false;
		}

		PayloadDto? dto;
		try
		{
			dto = JsonSerializer.Deserialize<PayloadDto>(payloadJson, ParseOptions);
		}
		catch (JsonException)
		{
			errorCode = "payload_json_invalid";
			return false;
		}

		if (dto is null)
		{
			errorCode = "payload_json_invalid";
			return false;
		}

		if (!TryParseO(dto.IssuedAtUtc, out var issued))
		{
			errorCode = "issued_at_invalid";
			return false;
		}

		DateTimeOffset? nbf = null;
		if (dto.NotBeforeUtc is not null)
		{
			if (!TryParseO(dto.NotBeforeUtc, out var nbfV))
			{
				errorCode = "not_before_invalid";
				return false;
			}
			nbf = nbfV;
		}

		DateTimeOffset? exp = null;
		if (dto.ExpiresAtUtc is not null)
		{
			if (!TryParseO(dto.ExpiresAtUtc, out var expV))
			{
				errorCode = "expires_at_invalid";
				return false;
			}
			exp = expV;
		}

		var subsystems = (IReadOnlyList<string>)(dto.Limits?.Subsystems ?? Array.Empty<string>());
		payload = new LicensePayload(
			dto.FormatVersion,
			dto.LicenseId ?? "",
			dto.InstallationId ?? "",
			dto.ProductCode ?? "",
			dto.LicenseKind ?? "",
			issued,
			nbf,
			exp,
			dto.GraceDays,
			dto.Sequence,
			new LicenseLimits(dto.Limits?.MaxCompanies, subsystems));
		return true;
	}

	private static bool TryParseO(string? value, out DateTimeOffset result)
	{
		result = default;
		if (string.IsNullOrEmpty(value))
			return false;
		return DateTimeOffset.TryParseExact(
			value,
			"O",
			System.Globalization.CultureInfo.InvariantCulture,
			System.Globalization.DateTimeStyles.RoundtripKind,
			out result);
	}

	private sealed class PayloadDto
	{
		public int FormatVersion { get; set; }
		public string? LicenseId { get; set; }
		public string? InstallationId { get; set; }
		public string? ProductCode { get; set; }
		public string? LicenseKind { get; set; }
		public string? IssuedAtUtc { get; set; }
		public string? NotBeforeUtc { get; set; }
		public string? ExpiresAtUtc { get; set; }
		public int GraceDays { get; set; }
		public int Sequence { get; set; }
		public LimitsDto? Limits { get; set; }
	}

	private sealed class LimitsDto
	{
		public int? MaxCompanies { get; set; }
		public string[]? Subsystems { get; set; }
	}
}
