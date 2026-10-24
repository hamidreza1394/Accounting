namespace Faranou.Licensing.Contracts;

public sealed class LicenseValidationResult
{
	private LicenseValidationResult(bool success, LicenseStatus status, LicensePayload? payload, string? errorCode)
	{
		Success = success;
		Status = status;
		Payload = payload;
		ErrorCode = errorCode;
	}

	public bool Success { get; }
	public LicenseStatus Status { get; }
	public LicensePayload? Payload { get; }
	/// <summary>Stable machine-readable code; no raw crypto exception text.</summary>
	public string? ErrorCode { get; }

	public static LicenseValidationResult Ok(LicenseStatus status, LicensePayload payload)
		=> new(true, status, payload, null);

	public static LicenseValidationResult Fail(string errorCode, LicenseStatus status = LicenseStatus.Invalid)
		=> new(false, status, null, errorCode);
}
