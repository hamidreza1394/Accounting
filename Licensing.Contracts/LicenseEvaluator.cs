namespace Faranou.Licensing.Contracts;

/// <summary>
/// Pure evaluation of a structurally valid, signature-verified payload against time.
/// Uses injected <see cref="TimeProvider"/> only (OD-05).
/// High-water clock anti-rollback is a host concern and is not implemented here.
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
	{
		ArgumentNullException.ThrowIfNull(payload);
		if (!string.Equals(payload.InstallationId, localInstallationId, StringComparison.Ordinal))
			return LicenseStatus.Invalid;

		var structural = StructuralValidation.ValidatePayload(payload);
		if (structural.Count > 0)
			return LicenseStatus.Invalid;

		var now = _time.GetUtcNow();

		if (payload.NotBeforeUtc is { } nbf && now < nbf)
			return LicenseStatus.Invalid;

		if (payload.LicenseKind == LicensePayload.KindPerpetual || payload.ExpiresAtUtc is null)
			return LicenseStatus.Valid;

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
