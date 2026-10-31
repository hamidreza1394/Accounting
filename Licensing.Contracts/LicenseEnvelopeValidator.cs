namespace Faranou.Licensing.Contracts;

/// <summary>
/// Single safe entry point for untrusted envelopes:
/// size/structure → algorithm allowlist → kid trust → signature on exact PayloadJson bytes
/// → parse that same JSON → structural payload rules → time evaluation.
/// The evaluated payload is always the one parsed from the signed string.
/// </summary>
public sealed class LicenseEnvelopeValidator
{
	private readonly ITrustedKeyResolver _keys;
	private readonly ILicenseSignatureVerifier _verifier;
	private readonly LicenseEvaluator _evaluator;

	public LicenseEnvelopeValidator(
		ITrustedKeyResolver keys,
		ILicenseSignatureVerifier verifier,
		LicenseEvaluator evaluator)
	{
		_keys = keys ?? throw new ArgumentNullException(nameof(keys));
		_verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
		_evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
	}

	public LicenseValidationResult Validate(LicenseEnvelope envelope, string localInstallationId)
		=> Validate(envelope, localInstallationId, localInstallationKeyFingerprint: null);

	public LicenseValidationResult Validate(
		LicenseEnvelope envelope,
		string localInstallationId,
		string? localInstallationKeyFingerprint)
	{
		if (envelope is null)
			return LicenseValidationResult.Fail("envelope_null");

		var structural = StructuralValidation.ValidateEnvelopeStructure(envelope);
		if (structural.Count > 0)
			return LicenseValidationResult.Fail("envelope_structure_invalid");

		if (!LicenseAlgorithms.IsAllowed(envelope.Algorithm)
		    || !string.Equals(envelope.Algorithm, _verifier.Algorithm, StringComparison.Ordinal))
			return LicenseValidationResult.Fail("algorithm_not_allowed");

		if (!_keys.TryResolve(envelope.Kid, out var trusted) || trusted is null)
			return LicenseValidationResult.Fail("kid_unknown");

		if (trusted.Retired)
			return LicenseValidationResult.Fail("kid_retired");

		if (!string.Equals(trusted.Algorithm, envelope.Algorithm, StringComparison.Ordinal))
			return LicenseValidationResult.Fail("kid_algorithm_mismatch");

		if (!_verifier.TryVerify(envelope, trusted.PublicKeySpki, out _))
			return LicenseValidationResult.Fail("signature_invalid");

		if (!CanonicalJson.TryParsePayload(envelope.PayloadJson, out var payload, out var parseError) || payload is null)
			return LicenseValidationResult.Fail(parseError ?? "payload_parse_failed");

		var payloadErrors = StructuralValidation.ValidatePayload(payload);
		if (payloadErrors.Count > 0)
			return LicenseValidationResult.Fail("payload_structure_invalid");

		if (envelope.FormatVersion != payload.FormatVersion)
			return LicenseValidationResult.Fail("envelope_payload_version_mismatch");

		if (payload.FormatVersion == 2)
		{
			if (string.IsNullOrWhiteSpace(localInstallationKeyFingerprint))
				return LicenseValidationResult.Fail("installation_key_fingerprint_required");
			if (!string.Equals(payload.InstallationKeyFingerprint, localInstallationKeyFingerprint, StringComparison.Ordinal))
				return LicenseValidationResult.Fail("installation_key_mismatch");
		}
		else if (!string.IsNullOrWhiteSpace(localInstallationKeyFingerprint))
		{
			return LicenseValidationResult.Fail("legacy_license_not_installation_bound");
		}

		var status = _evaluator.Evaluate(payload, localInstallationId, localInstallationKeyFingerprint);
		if (status == LicenseStatus.Invalid)
			return LicenseValidationResult.Fail("evaluation_invalid", LicenseStatus.Invalid);

		return LicenseValidationResult.Ok(status, payload);
	}
}
