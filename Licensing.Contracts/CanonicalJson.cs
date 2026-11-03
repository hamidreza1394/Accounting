using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Project-specific deterministic UTF-8 JSON for signing (NOT RFC 8785).
/// Payload v1 retains its historical property order/bytes. Payload v2 additionally sorts company
/// references, modules, and feature codes using Ordinal comparison.
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

	private static readonly JsonSerializerOptions StrictV2ParseOptions = new(ParseOptions)
	{
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
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
				Subsystems = subsystems.OrderBy(code => code, StringComparer.Ordinal).ToArray()
			}
		};

		if (payload.FormatVersion >= LicensePayload.CurrentFormatVersion)
		{
			dto.SupportUntilUtc = payload.SupportUntilUtc?.ToUniversalTime().ToString("O");
			dto.UpdatesUntilUtc = payload.UpdatesUntilUtc?.ToUniversalTime().ToString("O");
			dto.CompanyEntitlements = payload.CompanyEntitlements?
				.Select(company => company is null ? null : new CompanyEntitlementDto
				{
					CompanyReference = company.CompanyReference,
					Modules = company.Modules?.OrderBy(code => code, StringComparer.Ordinal).ToArray(),
					MaxUsers = company.MaxUsers,
					Features = company.Features?
						.Select(feature => feature is null ? null : new FeatureEntitlementDto
						{
							Code = feature.Code,
							Type = feature.Type,
							Enabled = feature.Enabled,
							MaxCount = feature.MaxCount,
							EnumValue = feature.EnumValue
						})
						.OrderBy(feature => feature?.Code, StringComparer.Ordinal)
						.ToArray()
				})
				.OrderBy(company => company?.CompanyReference, StringComparer.Ordinal)
				.ToArray();
		}

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
		int formatVersion;
		try
		{
			using var document = JsonDocument.Parse(payloadJson, new JsonDocumentOptions { MaxDepth = 8 });
			if (document.RootElement.ValueKind != JsonValueKind.Object ||
				!document.RootElement.TryGetProperty("formatVersion", out var versionElement) ||
				!versionElement.TryGetInt32(out formatVersion))
			{
				errorCode = "payload_format_version_invalid";
				return false;
			}

			var parseOptions = formatVersion >= LicensePayload.CurrentFormatVersion
				? StrictV2ParseOptions
				: ParseOptions;
			dto = JsonSerializer.Deserialize<PayloadDto>(payloadJson, parseOptions);
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
			if (!TryParseO(dto.NotBeforeUtc, out var nbfValue))
			{
				errorCode = "not_before_invalid";
				return false;
			}
			nbf = nbfValue;
		}

		DateTimeOffset? exp = null;
		if (dto.ExpiresAtUtc is not null)
		{
			if (!TryParseO(dto.ExpiresAtUtc, out var expValue))
			{
				errorCode = "expires_at_invalid";
				return false;
			}
			exp = expValue;
		}

		DateTimeOffset? supportUntil = null;
		if (dto.FormatVersion >= LicensePayload.CurrentFormatVersion && dto.SupportUntilUtc is not null)
		{
			if (!TryParseO(dto.SupportUntilUtc, out var supportValue))
			{
				errorCode = "support_until_invalid";
				return false;
			}
			supportUntil = supportValue;
		}

		DateTimeOffset? updatesUntil = null;
		if (dto.FormatVersion >= LicensePayload.CurrentFormatVersion && dto.UpdatesUntilUtc is not null)
		{
			if (!TryParseO(dto.UpdatesUntilUtc, out var updatesValue))
			{
				errorCode = "updates_until_invalid";
				return false;
			}
			updatesUntil = updatesValue;
		}

		var subsystems = (IReadOnlyList<string>)(dto.Limits?.Subsystems ?? Array.Empty<string>());
		var companies = dto.FormatVersion >= LicensePayload.CurrentFormatVersion
			? dto.CompanyEntitlements?
				.Select(company => company is null
					? null
					: new CompanyLicenseEntitlement(
						company.CompanyReference,
						company.Modules,
						company.MaxUsers,
						company.Features?
							.Select(feature => feature is null
								? null
								: new LicenseFeatureEntitlement(
									feature.Code,
									feature.Type,
									feature.Enabled,
									feature.MaxCount,
									feature.EnumValue))
							.ToArray()))
				.ToArray()
			: null;

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
			new LicenseLimits(dto.Limits?.MaxCompanies, subsystems))
		{
			SupportUntilUtc = supportUntil,
			UpdatesUntilUtc = updatesUntil,
			CompanyEntitlements = companies
		};
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
		public string? SupportUntilUtc { get; set; }
		public string? UpdatesUntilUtc { get; set; }
		public int GraceDays { get; set; }
		public int Sequence { get; set; }
		public LimitsDto? Limits { get; set; }
		public CompanyEntitlementDto?[]? CompanyEntitlements { get; set; }
	}

	private sealed class LimitsDto
	{
		public int? MaxCompanies { get; set; }
		public string[]? Subsystems { get; set; }
	}

	private sealed class CompanyEntitlementDto
	{
		public string? CompanyReference { get; set; }
		public string[]? Modules { get; set; }
		public int MaxUsers { get; set; }
		public FeatureEntitlementDto?[]? Features { get; set; }
	}

	private sealed class FeatureEntitlementDto
	{
		public string? Code { get; set; }
		public string? Type { get; set; }
		public bool? Enabled { get; set; }
		public int? MaxCount { get; set; }
		public string? EnumValue { get; set; }
	}
}
