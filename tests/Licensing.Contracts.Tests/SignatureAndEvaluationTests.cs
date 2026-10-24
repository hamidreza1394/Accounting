using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

public sealed class SignatureAndEvaluationTests
{
	[Fact]
	public void RoundTrip_SignAndVerify_Succeeds()
	{
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var pub = ecdsa.ExportSubjectPublicKeyInfo();
		var payload = SamplePayload();
		var json = CanonicalJson.SerializePayload(payload);
		var data = CanonicalJson.GetUtf8Bytes(json);
		var sig = ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
		var envelope = new LicenseEnvelope(
			LicenseAlgorithms.CurrentFormatVersion,
			"test-kid-1",
			LicenseAlgorithms.EcdsaP256Sha256,
			json,
			Convert.ToBase64String(sig));

		var verifier = new EcdsaP256Sha256Verifier();
		Assert.True(verifier.TryVerify(envelope, pub, out var error), error);
		Assert.Empty(StructuralValidation.ValidateEnvelopeStructure(envelope));
		Assert.Empty(StructuralValidation.ValidatePayload(payload));
	}

	[Fact]
	public void TamperedPayload_FailsVerification()
	{
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var pub = ecdsa.ExportSubjectPublicKeyInfo();
		var payload = SamplePayload();
		var json = CanonicalJson.SerializePayload(payload);
		var sig = ecdsa.SignData(CanonicalJson.GetUtf8Bytes(json), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
		var tampered = json.Replace("inst-001", "inst-999", StringComparison.Ordinal);
		var envelope = new LicenseEnvelope(1, "k", LicenseAlgorithms.EcdsaP256Sha256, tampered, Convert.ToBase64String(sig));
		Assert.False(new EcdsaP256Sha256Verifier().TryVerify(envelope, pub, out _));
	}

	[Fact]
	public void WrongKey_FailsVerification()
	{
		using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var payload = SamplePayload();
		var json = CanonicalJson.SerializePayload(payload);
		var sig = signer.SignData(CanonicalJson.GetUtf8Bytes(json), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
		var envelope = new LicenseEnvelope(1, "k", LicenseAlgorithms.EcdsaP256Sha256, json, Convert.ToBase64String(sig));
		Assert.False(new EcdsaP256Sha256Verifier().TryVerify(envelope, other.ExportSubjectPublicKeyInfo(), out _));
	}

	[Fact]
	public void Evaluator_TermLicense_Transitions()
	{
		var payload = SamplePayload() with
		{
			LicenseKind = LicensePayload.KindTerm,
			NotBeforeUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
			ExpiresAtUtc = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
			GraceDays = 10
		};
		var frozen = new FakeTime(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero));
		Assert.Equal(LicenseStatus.Valid, new LicenseEvaluator(frozen).Evaluate(payload, "inst-001"));

		frozen = new FakeTime(new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero));
		Assert.Equal(LicenseStatus.ExpiringSoon, new LicenseEvaluator(frozen).Evaluate(payload, "inst-001"));

		frozen = new FakeTime(new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero));
		Assert.Equal(LicenseStatus.Grace, new LicenseEvaluator(frozen).Evaluate(payload, "inst-001"));

		frozen = new FakeTime(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
		Assert.Equal(LicenseStatus.Restricted, new LicenseEvaluator(frozen).Evaluate(payload, "inst-001"));
	}

	[Fact]
	public void Evaluator_WrongInstallation_IsInvalid()
	{
		var payload = SamplePayload();
		var status = new LicenseEvaluator(TimeProvider.System).Evaluate(payload, "other-install");
		Assert.Equal(LicenseStatus.Invalid, status);
	}

	[Fact]
	public void ActivationRequest_RejectsInvalidFields()
	{
		var bad = new ActivationRequest(0, "x", "", "", default, new string('a', 500));
		var errors = StructuralValidation.ValidateActivationRequest(bad);
		Assert.NotEmpty(errors);
	}

	[Fact]
	public void CanonicalJson_IsStable()
	{
		var a = CanonicalJson.SerializePayload(SamplePayload());
		var b = CanonicalJson.SerializePayload(SamplePayload());
		Assert.Equal(a, b);
		Assert.Contains("\"formatVersion\":1", a, StringComparison.Ordinal);
	}

	private static LicensePayload SamplePayload() => new(
		FormatVersion: LicenseAlgorithms.CurrentFormatVersion,
		LicenseId: "lic-00000001",
		InstallationId: "inst-001",
		ProductCode: "faranou-accounting",
		LicenseKind: LicensePayload.KindPerpetual,
		IssuedAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
		NotBeforeUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
		ExpiresAtUtc: null,
		GraceDays: 30,
		Sequence: 1,
		Limits: new LicenseLimits(5, new[] { "finance" }));

	private sealed class FakeTime : TimeProvider
	{
		private readonly DateTimeOffset _utc;
		public FakeTime(DateTimeOffset utc) => _utc = utc;
		public override DateTimeOffset GetUtcNow() => _utc;
	}
}
