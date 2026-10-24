using System.Text.RegularExpressions;

namespace Faranou.Licensing.Contracts;

public static class StructuralValidation
{
	private static readonly Regex IdPattern = new(@"^[A-Za-z0-9._-]{8,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex CodePattern = new(@"^[A-Za-z0-9._-]{1,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public static IReadOnlyList<string> ValidateActivationRequest(ActivationRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		var errors = new List<string>();
		if (request.FormatVersion < 1)
			errors.Add("formatVersion must be >= 1");
		if (!IdPattern.IsMatch(request.InstallationId))
			errors.Add("installationId is invalid");
		if (!CodePattern.IsMatch(request.ProductCode))
			errors.Add("productCode is invalid");
		if (string.IsNullOrWhiteSpace(request.ProductVersion) || request.ProductVersion.Length > 64)
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
		if (payload.FormatVersion != LicenseAlgorithms.CurrentFormatVersion)
			errors.Add($"unsupported formatVersion: {payload.FormatVersion}");
		if (!IdPattern.IsMatch(payload.LicenseId))
			errors.Add("licenseId is invalid");
		if (!IdPattern.IsMatch(payload.InstallationId))
			errors.Add("installationId is invalid");
		if (!CodePattern.IsMatch(payload.ProductCode))
			errors.Add("productCode is invalid");
		if (payload.LicenseKind is not (LicensePayload.KindPerpetual or LicensePayload.KindTerm))
			errors.Add("licenseKind must be perpetual or term");
		if (payload.GraceDays < 0 || payload.GraceDays > LicensePayload.MaxGraceDaysCeiling)
			errors.Add("graceDays out of range");
		if (payload.Sequence < 1)
			errors.Add("sequence must be >= 1");
		if (payload.LicenseKind == LicensePayload.KindTerm && payload.ExpiresAtUtc is null)
			errors.Add("term license requires expiresAtUtc");
		if (payload.Limits.MaxCompanies is < 1)
			errors.Add("maxCompanies must be >= 1 when set");
		foreach (var s in payload.Limits.Subsystems)
		{
			if (!CodePattern.IsMatch(s))
				errors.Add($"invalid subsystem code: {s}");
		}
		return errors;
	}

	public static IReadOnlyList<string> ValidateEnvelopeStructure(LicenseEnvelope envelope)
	{
		ArgumentNullException.ThrowIfNull(envelope);
		var errors = new List<string>();
		if (envelope.FormatVersion != LicenseAlgorithms.CurrentFormatVersion)
			errors.Add($"unsupported envelope formatVersion: {envelope.FormatVersion}");
		if (string.IsNullOrWhiteSpace(envelope.Kid) || envelope.Kid.Length > 64)
			errors.Add("kid is invalid");
		if (string.IsNullOrWhiteSpace(envelope.Algorithm))
			errors.Add("algorithm is required");
		if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
			errors.Add("payloadJson is required");
		if (string.IsNullOrWhiteSpace(envelope.SignatureBase64))
			errors.Add("signatureBase64 is required");
		try
		{
			_ = Convert.FromBase64String(envelope.SignatureBase64);
		}
		catch
		{
			errors.Add("signatureBase64 is not valid Base64");
		}
		return errors;
	}
}
