namespace Faranou.Licensing.Contracts;

/// <summary>
/// Time/status evaluation only. Must not be used on unsigned data in production paths —
/// use <see cref="LicenseEnvelopeValidator"/> instead.
/// </summary>
public sealed class LicenseEvaluator
{
	private readonly TimeProvider _time;
	private readonly TimeSpan _expiringSoonWindow;

	public LicenseEvaluator(TimeProvider time, TimeSpan? expiringSoonWindow = null)
	{
		_time = time ?? throw new ArgumentNullException(nameof(time));
		_expiringSoonWindow = expiringSoonWindow ?? TimeSpan.FromDays(14);
	}

	public LicenseStatus Evaluate(LicensePayload payload, string localInstallationId)
		=> Evaluate(payload, localInstallationId, localInstallationKeyFingerprint: null);

	public LicenseStatus Evaluate(
		LicensePayload payload,
		string localInstallationId,
		string? localInstallationKeyFingerprint)
	{
		ArgumentNullException.ThrowIfNull(payload);
		if (string.IsNullOrEmpty(localInstallationId)
		    || !string.Equals(payload.InstallationId, localInstallationId, StringComparison.Ordinal))
			return LicenseStatus.Invalid;
		if (payload.FormatVersion == 2 &&
		    (string.IsNullOrWhiteSpace(localInstallationKeyFingerprint) ||
		     !string.Equals(payload.InstallationKeyFingerprint, localInstallationKeyFingerprint, StringComparison.Ordinal)))
			return LicenseStatus.Invalid;
		if (payload.FormatVersion == 1 && !string.IsNullOrWhiteSpace(localInstallationKeyFingerprint))
			return LicenseStatus.Invalid;

		var structural = StructuralValidation.ValidatePayload(payload);
		if (structural.Count > 0)
			return LicenseStatus.Invalid;

		var now = _time.GetUtcNow();

		if (payload.NotBeforeUtc is { } nbf && now < nbf)
			return LicenseStatus.Invalid;

		if (payload.LicenseKind == LicensePayload.KindPerpetual)
			return LicenseStatus.Valid;

		if (payload.ExpiresAtUtc is null)
			return LicenseStatus.Invalid;

		var expires = payload.ExpiresAtUtc.Value;
		if (now <= expires)
		{
			if (expires - now <= _expiringSoonWindow)
				return LicenseStatus.ExpiringSoon;
			return LicenseStatus.Valid;
		}

		var graceEnd = expires.AddDays(payload.GraceDays);
		if (now <= graceEnd)
			return LicenseStatus.Grace;

		return LicenseStatus.Restricted;
	}
}
