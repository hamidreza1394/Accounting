using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

public sealed class EnvelopeValidatorTests
{
	[Fact]
	public void ValidEnvelope_ReturnsPayloadFromSignedJson()
	{
		var (env, resolver, _) = CreateSigned();
		var result = NewValidator(resolver).Validate(env, "inst-001");
		Assert.True(result.Success, result.ErrorCode);
		Assert.Equal(LicenseStatus.Valid, result.Status);
		Assert.NotNull(result.Payload);
		Assert.Equal("lic-00000001", result.Payload!.LicenseId);
	}

	[Fact]
	public void UnknownKid_Fails()
	{
		var (env, resolver, _) = CreateSigned();
		env = env with { Kid = "unknown-kid-xx" };
		var result = NewValidator(resolver).Validate(env, "inst-001");
		Assert.False(result.Success);
		Assert.Equal("kid_unknown", result.ErrorCode);
	}

	[Fact]
	public void RetiredKid_Fails()
	{
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var pub = ecdsa.ExportSubjectPublicKeyInfo();
		var payload = Sample();
		var json = CanonicalJson.SerializePayload(payload);
		var sig = Sign(ecdsa, json);
		var env = new LicenseEnvelope(1, "kid-retired", LicenseAlgorithms.EcdsaP256Sha256, json, Convert.ToBase64String(sig));
		var resolver = new StaticTrustedKeyResolver(new[]
		{
			new TrustedPublicKey("kid-retired", LicenseAlgorithms.EcdsaP256Sha256, pub, retired: true)
		});
		var result = NewValidator(resolver).Validate(env, "inst-001");
		Assert.False(result.Success);
		Assert.Equal("kid_retired", result.ErrorCode);
	}

	[Fact]
	public void TamperedPayload_Fails_EvenIfSeparatelyEvaluable()
	{
		var (env, resolver, _) = CreateSigned();
		var tampered = env.PayloadJson.Replace("inst-001", "inst-999", StringComparison.Ordinal);
		env = env with { PayloadJson = tampered };
		var result = NewValidator(resolver).Validate(env, "inst-999");
		Assert.False(result.Success);
		Assert.Equal("signature_invalid", result.ErrorCode);
	}

	[Fact]
	public void OversizedPayload_Fails()
	{
		var (env, resolver, _) = CreateSigned();
		env = env with { PayloadJson = new string('a', LicenseInputLimits.MaxPayloadJsonChars + 1) };
		var result = NewValidator(resolver).Validate(env, "inst-001");
		Assert.False(result.Success);
		Assert.Equal("envelope_structure_invalid", result.ErrorCode);
	}

	[Fact]
	public void UnknownAlgorithm_Fails()
	{
		var (env, resolver, _) = CreateSigned();
		env = env with { Algorithm = "RSA-PSS-4096" };
		Assert.False(NewValidator(resolver).Validate(env, "inst-001").Success);
	}

	[Fact]
	public void WrongInstallation_Fails()
	{
		var (env, resolver, _) = CreateSigned();
		var result = NewValidator(resolver).Validate(env, "other-install-id");
		Assert.False(result.Success);
		Assert.Equal("evaluation_invalid", result.ErrorCode);
	}

	[Fact]
	public void PerpetualWithExpiry_FailsStructure()
	{
		var payload = Sample() with
		{
			LicenseKind = LicensePayload.KindPerpetual,
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddYears(1)
		};
		var errors = StructuralValidation.ValidatePayload(payload);
		Assert.Contains(errors, e => e.Contains("perpetual", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void TermWithoutExpiry_FailsStructure()
	{
		var payload = Sample() with { LicenseKind = LicensePayload.KindTerm, ExpiresAtUtc = null };
		var errors = StructuralValidation.ValidatePayload(payload);
		Assert.Contains(errors, e => e.Contains("expiresAtUtc", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void NotBeforeAfterExpires_Fails()
	{
		var payload = Sample() with
		{
			LicenseKind = LicensePayload.KindTerm,
			NotBeforeUtc = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
			ExpiresAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
		};
		Assert.NotEmpty(StructuralValidation.ValidatePayload(payload));
	}

	[Fact]
	public void InvalidBase64Signature_Fails()
	{
		var (env, resolver, _) = CreateSigned();
		env = env with { SignatureBase64 = "%%%not-base64%%%" };
		Assert.False(NewValidator(resolver).Validate(env, "inst-001").Success);
	}

	[Fact]
	public void CanonicalJson_FixedVector_MatchesExpectedPrefix()
	{
		var payload = Sample();
		var json = CanonicalJson.SerializePayload(payload);
		Assert.StartsWith("{\"formatVersion\":1,", json, StringComparison.Ordinal);
		Assert.Contains("\"licenseId\":\"lic-00000001\"", json, StringComparison.Ordinal);
		Assert.Contains("\"installationId\":\"inst-001\"", json, StringComparison.Ordinal);
		Assert.Contains("\"licenseKind\":\"perpetual\"", json, StringComparison.Ordinal);
		Assert.DoesNotContain("\n", json, StringComparison.Ordinal);
		Assert.True(CanonicalJson.TryParsePayload(json, out var parsed, out _));
		Assert.Equal(payload.LicenseId, parsed!.LicenseId);
		Assert.Equal(payload.InstallationId, parsed.InstallationId);
	}

	[Fact]
	public void NullSubsystems_HandledInValidation()
	{
		var json = """{"formatVersion":1,"licenseId":"lic-00000001","installationId":"inst-001","productCode":"faranou-accounting","licenseKind":"perpetual","issuedAtUtc":"2026-01-01T00:00:00.0000000+00:00","graceDays":30,"sequence":1,"limits":{"maxCompanies":5}}""";
		Assert.True(CanonicalJson.TryParsePayload(json, out var p, out _));
		Assert.NotNull(p!.Limits.Subsystems);
	}

	private static LicenseEnvelopeValidator NewValidator(ITrustedKeyResolver resolver)
		=> new(resolver, new EcdsaP256Sha256Verifier(), new LicenseEvaluator(TimeProvider.System));

	private static (LicenseEnvelope Env, ITrustedKeyResolver Resolver, ECDsa Key) CreateSigned()
	{
		var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var pub = ecdsa.ExportSubjectPublicKeyInfo();
		var payload = Sample();
		var json = CanonicalJson.SerializePayload(payload);
		var sig = Sign(ecdsa, json);
		var env = new LicenseEnvelope(1, "test-kid-1", LicenseAlgorithms.EcdsaP256Sha256, json, Convert.ToBase64String(sig));
		var resolver = new StaticTrustedKeyResolver(new[]
		{
			new TrustedPublicKey("test-kid-1", LicenseAlgorithms.EcdsaP256Sha256, pub)
		});
		return (env, resolver, ecdsa);
	}

	private static byte[] Sign(ECDsa ecdsa, string json)
		=> ecdsa.SignData(Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

	private static LicensePayload Sample() => new(
		1, "lic-00000001", "inst-001", "faranou-accounting", LicensePayload.KindPerpetual,
		new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
		new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
		null, 30, 1, new LicenseLimits(5, new[] { "finance" }));
}
