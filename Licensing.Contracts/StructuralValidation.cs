using System.Text.RegularExpressions;

namespace Faranou.Licensing.Contracts;

public static class StructuralValidation
{
	private static readonly Regex IdPattern = new(
		@"^[A-Za-z0-9._-]{8," + LicenseInputLimits.MaxIdChars + "}$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex CodePattern = new(
		@"^[A-Za-z0-9._-]{1," + LicenseInputLimits.MaxCodeChars + "}$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex InstallationKeyFingerprintPattern = new(
		"^[a-f0-9]{64}$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex KidPattern = new(
		@"^[A-Za-z0-9._-]{1," + LicenseInputLimits.MaxKidChars + "}$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public static IReadOnlyList<string> ValidateActivationRequest(ActivationRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		var errors = new List<string>();
		if (request.FormatVersion < 1)
			errors.Add("formatVersion must be >= 1");
		if (request.InstallationId is null || !IdPattern.IsMatch(request.InstallationId))
			errors.Add("installationId is invalid");
		if (request.ProductCode is null || !CodePattern.IsMatch(request.ProductCode))
			errors.Add("productCode is invalid");
		if (string.IsNullOrWhiteSpace(request.ProductVersion) || request.ProductVersion.Length > LicenseInputLimits.MaxCodeChars)
			errors.Add("productVersion is invalid");
		if (request.CreatedAtUtc == default)
			errors.Add("createdAtUtc is required");
		if (request.CustomerHint is { Length: > ActivationRequest.MaxCustomerHintLength })
			errors.Add("customerHint exceeds max length");
		return errors;
	}

	public static IReadOnlyList<string> ValidatePayload(LicensePayload payload)
	{
		ArgumentNullException.ThrowIfNull(payload);
		var errors = new List<string>();
		if (!LicenseAlgorithms.IsSupportedFormatVersion(payload.FormatVersion))
			errors.Add($"unsupported formatVersion: {payload.FormatVersion}");
		if (payload.FormatVersion == 2 && !InstallationKeyFingerprintPattern.IsMatch(payload.InstallationKeyFingerprint ?? ""))
			errors.Add("installationKeyFingerprint must be a lowercase SHA-256 fingerprint for format v2");
		if (payload.FormatVersion == 1 && !string.IsNullOrEmpty(payload.InstallationKeyFingerprint))
			errors.Add("format v1 must not contain installationKeyFingerprint");
		if (payload.LicenseId is null || !IdPattern.IsMatch(payload.LicenseId))
			errors.Add("licenseId is invalid");
		if (payload.InstallationId is null || !IdPattern.IsMatch(payload.InstallationId))
			errors.Add("installationId is invalid");
		if (payload.ProductCode is null || !CodePattern.IsMatch(payload.ProductCode))
			errors.Add("productCode is invalid");
		if (payload.LicenseKind is not (LicensePayload.KindPerpetual or LicensePayload.KindTerm))
			errors.Add("licenseKind must be perpetual or term");
		if (payload.GraceDays < 0 || payload.GraceDays > LicensePayload.MaxGraceDaysCeiling)
			errors.Add("graceDays out of range");
		if (payload.Sequence < 1)
			errors.Add("sequence must be >= 1");

		if (payload.LicenseKind == LicensePayload.KindTerm)
		{
			if (payload.ExpiresAtUtc is null)
				errors.Add("term license requires expiresAtUtc");
		}
		else if (payload.LicenseKind == LicensePayload.KindPerpetual)
		{
			if (payload.ExpiresAtUtc is not null)
				errors.Add("perpetual license must not set expiresAtUtc");
		}

		if (payload.NotBeforeUtc is { } nbf && payload.ExpiresAtUtc is { } exp && nbf > exp)
			errors.Add("notBeforeUtc must not be after expiresAtUtc");

		if (payload.Limits is null)
			errors.Add("limits is required");
		else
		{
			if (payload.Limits.MaxCompanies is < 1)
				errors.Add("maxCompanies must be >= 1 when set");
			var subs = payload.Limits.Subsystems;
			if (subs is null)
				errors.Add("subsystems is required");
			else if (subs.Count > LicenseInputLimits.MaxSubsystemCount)
				errors.Add("too many subsystems");
			else
			{
				var seenSubsystems = new HashSet<string>(StringComparer.Ordinal);
				foreach (var s in subs)
				{
					if (s is null || !CodePattern.IsMatch(s) || s.Length > LicenseInputLimits.MaxSubsystemCodeChars)
					{
						errors.Add("invalid subsystem code");
						continue;
					}

					if (!LicenseSubsystemCodes.IsAllowed(s))
						errors.Add("unsupported subsystem code");
					if (!seenSubsystems.Add(s))
						errors.Add("duplicate subsystem code");
				}
			}
		}

		return errors;
	}

	public static IReadOnlyList<string> ValidateEnvelopeStructure(LicenseEnvelope envelope)
	{
		ArgumentNullException.ThrowIfNull(envelope);
		var errors = new List<string>();
		if (!LicenseAlgorithms.IsSupportedFormatVersion(envelope.FormatVersion))
			errors.Add($"unsupported envelope formatVersion: {envelope.FormatVersion}");
		if (envelope.Kid is null || !KidPattern.IsMatch(envelope.Kid))
			errors.Add("kid is invalid");
		if (string.IsNullOrWhiteSpace(envelope.Algorithm) || envelope.Algorithm.Length > LicenseInputLimits.MaxAlgorithmChars)
			errors.Add("algorithm is invalid");
		else if (!LicenseAlgorithms.IsAllowed(envelope.Algorithm))
			errors.Add("algorithm not allowed");
		if (string.IsNullOrEmpty(envelope.PayloadJson))
			errors.Add("payloadJson is required");
		else if (envelope.PayloadJson.Length > LicenseInputLimits.MaxPayloadJsonChars)
			errors.Add("payloadJson too large");
		if (string.IsNullOrEmpty(envelope.SignatureBase64))
			errors.Add("signatureBase64 is required");
		else if (envelope.SignatureBase64.Length > LicenseInputLimits.MaxSignatureBase64Chars)
			errors.Add("signatureBase64 too large");
		else
		{
			try
			{
				_ = Convert.FromBase64String(envelope.SignatureBase64);
			}
			catch
			{
				errors.Add("signatureBase64 is not valid Base64");
			}
		}
		return errors;
	}
}
