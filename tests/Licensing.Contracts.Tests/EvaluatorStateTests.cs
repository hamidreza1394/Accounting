using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

/// <summary>
/// Restores coverage removed in PR #74 (the D-026 time-state machine and the wrong-key check)
/// and adds the boundary and format-version cases required by ROADMAP phase 8 / L1.
/// Time is frozen through a fake <see cref="TimeProvider"/>; nothing depends on the real clock.
/// </summary>
public sealed class EvaluatorStateTests
{
	private const string Installation = "inst-001";

	// Term license: expires 2026-07-01T00:00:00Z, grace 10 days (grace ends 2026-07-11T00:00:00Z),
	// default "expiring soon" window of 14 days (starts 2026-06-17T00:00:00Z).
	[Theory]
	[InlineData("2026-06-01T00:00:00Z", LicenseStatus.Valid)]
	[InlineData("2026-06-16T23:59:59Z", LicenseStatus.Valid)]
	[InlineData("2026-06-17T00:00:00Z", LicenseStatus.ExpiringSoon)]
	[InlineData("2026-07-01T00:00:00Z", LicenseStatus.ExpiringSoon)]
	[InlineData("2026-07-01T00:00:01Z", LicenseStatus.Grace)]
	[InlineData("2026-07-11T00:00:00Z", LicenseStatus.Grace)]
	[InlineData("2026-07-11T00:00:01Z", LicenseStatus.Restricted)]
	[InlineData("2027-01-01T00:00:00Z", LicenseStatus.Restricted)]
	public void TermLicense_TransitionsAtExactBoundaries(string nowUtc, LicenseStatus expected)
	{
		var payload = TermPayload(graceDays: 10);
		Assert.Equal(expected, Evaluator(nowUtc).Evaluate(payload, Installation));
	}

	[Fact]
	public void TermLicense_WithZeroGrace_IsRestrictedRightAfterExpiry()
	{
		var payload = TermPayload(graceDays: 0);
		Assert.Equal(LicenseStatus.ExpiringSoon, Evaluator("2026-07-01T00:00:00Z").Evaluate(payload, Installation));
		Assert.Equal(LicenseStatus.Restricted, Evaluator("2026-07-01T00:00:01Z").Evaluate(payload, Installation));
	}

	[Fact]
	public void TermLicense_BeforeNotBefore_IsInvalid()
	{
		var payload = TermPayload(graceDays: 10) with
		{
			NotBeforeUtc = DateTimeOffset.Parse("2026-02-01T00:00:00Z")
		};
		Assert.Equal(LicenseStatus.Invalid, Evaluator("2026-01-15T00:00:00Z").Evaluate(payload, Installation));
		Assert.Equal(LicenseStatus.Valid, Evaluator("2026-02-01T00:00:00Z").Evaluate(payload, Installation));
	}

	[Fact]
	public void PerpetualLicense_StaysValid_FarInTheFuture()
	{
		var payload = TermPayload(graceDays: 10) with
		{
			LicenseKind = LicensePayload.KindPerpetual,
			ExpiresAtUtc = null
		};
		Assert.Equal(LicenseStatus.Valid, Evaluator("2100-01-01T00:00:00Z").Evaluate(payload, Installation));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("some-other-install")]
	public void Evaluate_WithMissingOrDifferentInstallation_IsInvalid(string? localInstallationId)
	{
		var payload = TermPayload(graceDays: 10);
		Assert.Equal(LicenseStatus.Invalid, Evaluator("2026-06-01T00:00:00Z").Evaluate(payload, localInstallationId!));
	}

	[Fact]
	public void WrongKey_FailsVerification()
	{
		using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var json = CanonicalJson.SerializePayload(TermPayload(graceDays: 10));
		var env = new LicenseEnvelope(1, "kid-wrong-key", LicenseAlgorithms.EcdsaP256Sha256, json, Sign(signer, json));

		// The trust store holds a different public key under the same kid.
		var resolver = new StaticTrustedKeyResolver(new[]
		{
			new TrustedPublicKey("kid-wrong-key", LicenseAlgorithms.EcdsaP256Sha256, other.ExportSubjectPublicKeyInfo())
		});

		var result = NewValidator(resolver, "2026-06-01T00:00:00Z").Validate(env, Installation);
		Assert.False(result.Success);
		Assert.Equal("signature_invalid", result.ErrorCode);
	}

	[Fact]
	public void TrustedKeyWithDifferentAlgorithm_IsRejected()
	{
		using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var json = CanonicalJson.SerializePayload(TermPayload(graceDays: 10));
		var env = new LicenseEnvelope(1, "kid-alg-mismatch", LicenseAlgorithms.EcdsaP256Sha256, json, Sign(key, json));
		var resolver = new StaticTrustedKeyResolver(new[]
		{
			new TrustedPublicKey("kid-alg-mismatch", "SOME-OTHER-ALGORITHM", key.ExportSubjectPublicKeyInfo())
		});

		var result = NewValidator(resolver, "2026-06-01T00:00:00Z").Validate(env, Installation);
		Assert.False(result.Success);
		Assert.Equal("kid_algorithm_mismatch", result.ErrorCode);
	}

	[Fact]
	public void UnknownEnvelopeFormatVersion_IsRejected()
	{
		var (env, resolver) = SignedEnvelope(TermPayload(graceDays: 10));
		var result = NewValidator(resolver, "2026-06-01T00:00:00Z").Validate(env with { FormatVersion = 2 }, Installation);
		Assert.False(result.Success);
		Assert.Equal("envelope_structure_invalid", result.ErrorCode);
	}

	[Fact]
	public void UnknownPayloadFormatVersion_IsRejected_EvenWhenCorrectlySigned()
	{
		var (env, resolver) = SignedEnvelope(TermPayload(graceDays: 10) with { FormatVersion = 2 });
		var result = NewValidator(resolver, "2026-06-01T00:00:00Z").Validate(env, Installation);
		Assert.False(result.Success);
		Assert.Equal("payload_structure_invalid", result.ErrorCode);
	}

	[Fact]
	public void SignatureWithWrongLength_IsRejected()
	{
		var (env, resolver) = SignedEnvelope(TermPayload(graceDays: 10));
		var shortSignature = Convert.ToBase64String(new byte[63]);
		var result = NewValidator(resolver, "2026-06-01T00:00:00Z").Validate(env with { SignatureBase64 = shortSignature }, Installation);
		Assert.False(result.Success);
		Assert.Equal("signature_invalid", result.ErrorCode);
	}

	[Fact]
	public void ExpiredTermLicense_ValidatedEndToEnd_ReportsRestrictedAsSuccess()
	{
		// A correctly signed but long-expired license is a valid envelope in the Restricted state (D-026):
		// it must not be reported as a validation failure, otherwise the app could not offer read-only mode.
		var (env, resolver) = SignedEnvelope(TermPayload(graceDays: 10));
		var result = NewValidator(resolver, "2027-01-01T00:00:00Z").Validate(env, Installation);
		Assert.True(result.Success, result.ErrorCode);
		Assert.Equal(LicenseStatus.Restricted, result.Status);
	}

	private static LicensePayload TermPayload(int graceDays) => new(
		FormatVersion: 1,
		LicenseId: "lic-00000002",
		InstallationId: Installation,
		ProductCode: "faranou-accounting",
		LicenseKind: LicensePayload.KindTerm,
		IssuedAtUtc: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
		NotBeforeUtc: null,
		ExpiresAtUtc: DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
		GraceDays: graceDays,
		Sequence: 1,
		Limits: new LicenseLimits(5, new[] { "finance" }));

	private static LicenseEvaluator Evaluator(string nowUtc) => new(new FakeTime(DateTimeOffset.Parse(nowUtc)));

	private static LicenseEnvelopeValidator NewValidator(ITrustedKeyResolver resolver, string nowUtc)
		=> new(resolver, new EcdsaP256Sha256Verifier(), Evaluator(nowUtc));

	private static (LicenseEnvelope Env, ITrustedKeyResolver Resolver) SignedEnvelope(LicensePayload payload)
	{
		var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var json = CanonicalJson.SerializePayload(payload);
		var env = new LicenseEnvelope(1, "kid-end-to-end", LicenseAlgorithms.EcdsaP256Sha256, json, Sign(key, json));
		var resolver = new StaticTrustedKeyResolver(new[]
		{
			new TrustedPublicKey("kid-end-to-end", LicenseAlgorithms.EcdsaP256Sha256, key.ExportSubjectPublicKeyInfo())
		});
		return (env, resolver);
	}

	private static string Sign(ECDsa key, string json)
		=> Convert.ToBase64String(key.SignData(
			Encoding.UTF8.GetBytes(json),
			HashAlgorithmName.SHA256,
			DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

	private sealed class FakeTime(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}
}
