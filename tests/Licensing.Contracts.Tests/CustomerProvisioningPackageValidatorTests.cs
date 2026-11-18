using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

public sealed class CustomerProvisioningPackageValidatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidPackage_VerifiesExactCanonicalPayload()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = Sample();
        var json = CustomerProvisioningCanonicalJson.Serialize(payload);
        var signature = signingKey.SignData(
            System.Text.Encoding.UTF8.GetBytes(json),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var package = new CustomerProvisioningPackage(
            1, "provisioning-key-1", LicenseAlgorithms.EcdsaP256Sha256, json, Convert.ToBase64String(signature));

        var valid = CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out var parsed, out var error);

        Assert.True(valid, error);
        Assert.NotNull(parsed);
        Assert.Equal(payload.RequestId, parsed!.RequestId);
        Assert.Equal(new[] { "finance", "inventory" }, parsed.Subsystems.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ModifiedPayload_FailsSignatureVerification()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var json = CustomerProvisioningCanonicalJson.Serialize(Sample());
        var signature = signingKey.SignData(
            System.Text.Encoding.UTF8.GetBytes(json),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var package = new CustomerProvisioningPackage(
            1, "provisioning-key-1", LicenseAlgorithms.EcdsaP256Sha256,
            json.Replace("Company Example", "Other Company", StringComparison.Ordinal),
            Convert.ToBase64String(signature));

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out _, out var error));
        Assert.Contains(error, new[] { "provisioning_payload_noncanonical", "provisioning_signature_invalid" });
    }

    [Fact]
    public void InvalidSignature_IsRejectedBeforePayloadParsing()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validJson = CustomerProvisioningCanonicalJson.Serialize(Sample());
        var signature = signingKey.SignData(
            System.Text.Encoding.UTF8.GetBytes(validJson),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var package = new CustomerProvisioningPackage(
            1, "provisioning-key-1", LicenseAlgorithms.EcdsaP256Sha256,
            "not-json", Convert.ToBase64String(signature));

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_signature_invalid", error);
    }

    [Fact]
    public void ExpiredPackage_IsRejected()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = Sample() with
        {
            IssuedAtUtc = Now.AddHours(-25),
            ExpiresAtUtc = Now.AddHours(-1)
        };
        var package = Sign(signingKey, payload);

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out _, out var error));
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void DuplicateSubsystems_AreRejected()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var package = Sign(signingKey, Sample() with { Subsystems = new[] { "finance", "finance" } });

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out _, out var error));
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void PayloadValidation_AcceptsValidCanonicalPayloadWithoutRequiringSignature()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(Sample());

        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error), error);
        Assert.NotNull(parsed);
        Assert.Equal(Sample().RequestId, parsed!.RequestId);
    }

    [Fact]
    public void PayloadValidation_RejectsInvalidCompanyType()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(Sample() with { CompanyType = "unknown" });

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void PayloadValidation_RejectsOversizedPayload()
    {
        var oversized = new string(' ', CustomerProvisioningPackageValidator.MaxPayloadJsonChars + 1);

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            oversized, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void PayloadValidation_RejectsUnknownSubsystemWithoutRequiringSignature()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(
            Sample() with { Subsystems = new[] { "future-module" } });

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void PayloadValidation_RejectsExpiredPayloadWithoutRequiringSignature()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(Sample() with
        {
            IssuedAtUtc = Now.AddHours(-25),
            ExpiresAtUtc = Now.AddHours(-1)
        });

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void PayloadValidation_RejectsNonCanonicalJson()
    {
        var canonical = CustomerProvisioningCanonicalJson.Serialize(Sample());
        var nonCanonical = canonical.Replace("\"Company Example\"", "\"Company Example\", \"extra\": true", StringComparison.Ordinal);

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            nonCanonical, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_payload_noncanonical", error);
    }

    [Fact]
    public void UnknownSubsystem_IsRejected()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var package = Sign(signingKey, Sample() with { Subsystems = new[] { "future-module" } });

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out _, out var error));
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void UnknownVersion_IsRejected()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var package = Sign(signingKey, Sample() with { SchemaVersion = 3 });

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out _, out var error));
        Assert.Equal("provisioning_payload_invalid", error);
    }

    [Fact]
    public void V1CanonicalPayload_RemainsCompatibleAndOmitsV2Fields()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(Sample());

        Assert.DoesNotContain("fiscalPeriodTitle", json, StringComparison.Ordinal);
        Assert.DoesNotContain("parentCompanyReference", json, StringComparison.Ordinal);
        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error), error);
        Assert.Equal(1, parsed!.SchemaVersion);
    }

    [Fact]
    public void V2SignedPackage_VerifiesAndReturnsPeriodMetadata()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var package = Sign(signingKey, SampleV2());

        Assert.True(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out var parsed, out var error), error);
        Assert.Equal(new DateOnly(2026, 1, 1), parsed!.FiscalPeriodStartDate);
        Assert.Equal(new DateOnly(2027, 1, 1), parsed.FiscalPeriodEndDate);
    }

    [Fact]
    public void V2Payload_RequiresFiscalPeriodAndValidParentRelationship()
    {
        var payload = SampleV2();
        var json = CustomerProvisioningCanonicalJson.Serialize(payload);

        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error), error);
        Assert.Equal(new DateOnly(2026, 1, 1), parsed!.FiscalPeriodStartDate);

        var missingPeriod = CustomerProvisioningCanonicalJson.Serialize(payload with { FiscalPeriodStartDate = null });
        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            missingPeriod, Now, out _, out _));

        var subsidiaryWithoutParent = CustomerProvisioningCanonicalJson.Serialize(payload with
        {
            CompanyType = "subsidiary",
            ParentCompanyReference = null
        });
        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            subsidiaryWithoutParent, Now, out _, out _));
    }

    [Fact]
    public void V2Payload_RejectsReversedFiscalPeriodDates()
    {
        var payload = SampleV2() with
        {
            FiscalPeriodStartDate = new DateOnly(2027, 1, 1),
            FiscalPeriodEndDate = new DateOnly(2026, 1, 1)
        };
        var json = CustomerProvisioningCanonicalJson.Serialize(payload);

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out _, out _));
    }


    [Fact]
    public void V3Payload_RequiresAValidInitialAdminEmailAndPreservesV2Compatibility()
    {
        var v3 = SampleV3();
        var json = CustomerProvisioningCanonicalJson.Serialize(v3);

        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error), error);
        Assert.Equal("admin@example.com", parsed!.InitialAdminEmail);

        var missingEmail = CustomerProvisioningCanonicalJson.Serialize(v3 with { InitialAdminEmail = null });
        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            missingEmail, Now, out _, out _));

        var malformedEmail = CustomerProvisioningCanonicalJson.Serialize(v3 with { InitialAdminEmail = "admin at example.com" });
        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            malformedEmail, Now, out _, out _));

        var v2 = SampleV2();
        var v2Json = CustomerProvisioningCanonicalJson.Serialize(v2);
        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            v2Json, Now, out var parsedV2, out var v2Error), v2Error);
        Assert.Null(parsedV2!.InitialAdminEmail);
    }

    [Fact]
    public void PlaintextPasswordIsNotPartOfProvisioningPayloadContract()
    {
        var names = typeof(CustomerProvisioningPayload).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain(names, name => name.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void PayloadValidation_RequiresCanonicalVendorRequestIdForParentReference()
    {
        var validReference = "a0b1c2d3-e4f5-4678-9012-abcdefabcdef";
        var valid = CustomerProvisioningCanonicalJson.Serialize(SampleV3() with
        {
            CompanyType = "subsidiary",
            ParentCompanyReference = validReference
        });
        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            valid, Now, out _, out var validError), validError);

        var uppercase = CustomerProvisioningCanonicalJson.Serialize(SampleV3() with
        {
            CompanyType = "subsidiary",
            ParentCompanyReference = validReference.ToUpperInvariant()
        });
        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            uppercase, Now, out _, out var uppercaseError), uppercaseError);

        foreach (var invalidReference in new[] { "parent-001", "{" + validReference + "}", "not-a-guid", Guid.Empty.ToString("D") })
        {
            var invalid = CustomerProvisioningCanonicalJson.Serialize(SampleV3() with
            {
                CompanyType = "subsidiary",
                ParentCompanyReference = invalidReference
            });
            Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
                invalid, Now, out var parsed, out _));
            Assert.Null(parsed);
        }
    }

    [Fact]
    public void V3CanonicalPayload_RemainsByteForByteUnchanged()
    {
        const string expected =
            "{\"schemaVersion\":3,\"requestId\":\"9b0d2c4d-53bc-4bb2-a22c-90b30f2b58e8\",\"customerReference\":\"customer-ref-001\",\"installationId\":\"installation-001\",\"companyName\":\"Company Example\",\"companyType\":\"independent\",\"initialAdminUserName\":\"initial-admin\",\"subsystems\":[\"finance\",\"inventory\"],\"issuedAtUtc\":\"2026-10-28T11:59:00.0000000\\u002B00:00\",\"expiresAtUtc\":\"2026-10-28T13:00:00.0000000\\u002B00:00\",\"nonce\":\"abcdefghijklmnopqrstuvwxyz0123456789_-\",\"fiscalPeriodTitle\":\"Fiscal year 1405\",\"fiscalPeriodStartDate\":\"2026-01-01\",\"fiscalPeriodEndDate\":\"2027-01-01\",\"parentCompanyReference\":null,\"initialAdminEmail\":\"admin@example.com\"}";

        var payload = SampleV3() with { FiscalPeriodTitle = "Fiscal year 1405" };
        Assert.Equal(expected, CustomerProvisioningCanonicalJson.Serialize(payload));
    }

    [Fact]
    public void V4Payload_RequiresCanonicalLicenseIdAndPositiveSequence()
    {
        var valid = CustomerProvisioningCanonicalJson.Serialize(SampleV4());
        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            valid, Now, out var parsed, out var error), error);
        Assert.Equal("0a5f7d20-3a2c-4c8d-9e1f-2a6b7c8d9e10", parsed!.LicenseId);
        Assert.Equal(7, parsed.Sequence);

        foreach (var invalid in new[]
        {
            SampleV4() with { LicenseId = null },
            SampleV4() with { LicenseId = "license-001" },
            SampleV4() with { LicenseId = Guid.Empty.ToString("D") },
            SampleV4() with { Sequence = null },
            SampleV4() with { Sequence = 0 },
            SampleV4() with { Sequence = -1 }
        })
        {
            Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
                CustomerProvisioningCanonicalJson.Serialize(invalid), Now, out var rejected, out _));
            Assert.Null(rejected);
        }
    }

    [Fact]
    public void V4SignedPackage_VerifiesLicenseBindingAndKeepsLegacySchemasUnchanged()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var package = Sign(signingKey, SampleV4());

        Assert.True(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out var parsed, out var error), error);
        Assert.Equal(SampleV4().LicenseId, parsed!.LicenseId);
        Assert.Equal(SampleV4().Sequence, parsed.Sequence);

        var legacyJson = CustomerProvisioningCanonicalJson.Serialize(SampleV3());
        var legacyWithLicenseFields = legacyJson[..^1] +
            ",\"licenseId\":\"0a5f7d20-3a2c-4c8d-9e1f-2a6b7c8d9e10\",\"sequence\":7}";
        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            legacyWithLicenseFields, Now, out _, out _));
    }

    // ---- Schema v5: per-Company companyReference (PR-A) ----

    private const string GoldenV1 =
        "{\"schemaVersion\":1,\"requestId\":\"9b0d2c4d-53bc-4bb2-a22c-90b30f2b58e8\",\"customerReference\":\"customer-ref-001\"," +
        "\"installationId\":\"installation-001\",\"companyName\":\"Company Example\",\"companyType\":\"independent\"," +
        "\"initialAdminUserName\":\"initial-admin\",\"subsystems\":[\"finance\",\"inventory\"]," +
        "\"issuedAtUtc\":\"2026-10-28T11:59:00.0000000\\u002B00:00\",\"expiresAtUtc\":\"2026-10-28T13:00:00.0000000\\u002B00:00\"," +
        "\"nonce\":\"abcdefghijklmnopqrstuvwxyz0123456789_-\"";

    private const string GoldenV2Tail =
        ",\"fiscalPeriodTitle\":\"FY 2026\",\"fiscalPeriodStartDate\":\"2026-01-01\",\"fiscalPeriodEndDate\":\"2027-01-01\",\"parentCompanyReference\":null";

    private const string GoldenV3Tail = ",\"initialAdminEmail\":\"admin@example.com\"";

    private const string GoldenV4Tail = ",\"licenseId\":\"0a5f7d20-3a2c-4c8d-9e1f-2a6b7c8d9e10\",\"sequence\":7";

    private const string GoldenV5Tail = ",\"companyReference\":\"company-ref-0001\"";

    private static string Golden(int version) =>
        GoldenV1.Replace("\"schemaVersion\":1", $"\"schemaVersion\":{version}", StringComparison.Ordinal) +
        (version >= 2 ? GoldenV2Tail : "") +
        (version >= 3 ? GoldenV3Tail : "") +
        (version >= 4 ? GoldenV4Tail : "") +
        (version >= 5 ? GoldenV5Tail : "") +
        "}";

    private static CustomerProvisioningPayload GoldenSample(int version) => version switch
    {
        1 => Sample(),
        2 => SampleV2() with { FiscalPeriodTitle = "FY 2026" },
        3 => SampleV3() with { FiscalPeriodTitle = "FY 2026" },
        4 => SampleV4() with { FiscalPeriodTitle = "FY 2026" },
        _ => SampleV5() with { FiscalPeriodTitle = "FY 2026" }
    };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void CanonicalJson_MatchesGoldenBytesForEverySchemaVersion(int version)
    {
        Assert.Equal(Golden(version), CustomerProvisioningCanonicalJson.Serialize(GoldenSample(version)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void LegacyVersions_IgnoreCompanyReferenceInCanonicalJson(int version)
    {
        var with = GoldenSample(version) with { CompanyReference = "company-ref-0001" };

        Assert.Equal(Golden(version), CustomerProvisioningCanonicalJson.Serialize(with));
    }

    [Fact]
    public void V5Payload_WithCompanyReference_IsValidWhenOptedIn()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(SampleV5());

        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var parsed, out var error, allowCompanyReference: true), error);
        Assert.Equal(5, parsed!.SchemaVersion);
        Assert.Equal("company-ref-0001", parsed.CompanyReference);
        Assert.Equal(SampleV5().LicenseId, parsed.LicenseId);
    }

    [Fact]
    public void V5SignedPackage_VerifiesWhenOptedInAndIsRejectedByDefault()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var package = Sign(signingKey, SampleV5());

        Assert.True(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out var parsed, out var error,
            allowCompanyReference: true), error);
        Assert.Equal("company-ref-0001", parsed!.CompanyReference);

        Assert.False(CustomerProvisioningPackageValidator.TryValidateAndVerify(
            package, signingKey.ExportSubjectPublicKeyInfo(), Now, out var rejected, out var defaultError));
        Assert.Null(rejected);
        Assert.Equal("provisioning_payload_invalid", defaultError);
    }

    [Fact]
    public void V5Payload_IsRejectedByDefaultWithoutOptIn()
    {
        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            CustomerProvisioningCanonicalJson.Serialize(SampleV5()), Now, out var parsed, out _));
        Assert.Null(parsed);
    }

    [Fact]
    public void V5Payload_WithoutCompanyReference_IsRejected()
    {
        foreach (var missing in new string?[] { null, "", "   " })
        {
            Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
                CustomerProvisioningCanonicalJson.Serialize(SampleV5() with { CompanyReference = missing }),
                Now, out var parsed, out _, allowCompanyReference: true));
            Assert.Null(parsed);
        }
    }

    [Theory]
    [InlineData("short")]
    [InlineData("has space in it")]
    [InlineData("slash/not/allowed")]
    [InlineData("unicode-\u0633\u0644\u0627\u0645-ref")]
    public void V5Payload_WithInvalidCompanyReference_IsRejected(string value)
    {
        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            CustomerProvisioningCanonicalJson.Serialize(SampleV5() with { CompanyReference = value }),
            Now, out _, out _, allowCompanyReference: true));
        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            CustomerProvisioningCanonicalJson.Serialize(SampleV5() with { CompanyReference = new string('a', 129) }),
            Now, out _, out _, allowCompanyReference: true));
    }

    [Theory]
    [InlineData("company-ref-0001")]
    [InlineData("0a5f7d20-3a2c-4c8d-9e1f-2a6b7c8d9e10")]
    [InlineData("ABCDEFGH")]
    [InlineData("a.b_c-d.e")]
    public void V5Payload_AcceptsAnyValueMatchingTheCurrentContractPattern(string value)
    {
        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            CustomerProvisioningCanonicalJson.Serialize(SampleV5() with { CompanyReference = value }),
            Now, out var parsed, out var error, allowCompanyReference: true), error);
        Assert.Equal(value, parsed!.CompanyReference);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void LegacyVersions_RejectCompanyReferenceEvenWhenOptedIn(int version)
    {
        var legacyJson = CustomerProvisioningCanonicalJson.Serialize(GoldenSample(version));
        var withReference = legacyJson[..^1] + ",\"companyReference\":\"company-ref-0001\"}";

        foreach (var optIn in new[] { false, true })
        {
            Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
                withReference, Now, out var parsed, out _, allowCompanyReference: optIn));
            Assert.Null(parsed);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void LegacyVersions_StillValidateIdenticallyWhenOptedIn(int version)
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(GoldenSample(version));

        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var plain, out var plainError), plainError);
        Assert.True(CustomerProvisioningPackageValidator.TryValidatePayload(
            json, Now, out var optedIn, out var optedInError, allowCompanyReference: true), optedInError);
        Assert.Equal(
            CustomerProvisioningCanonicalJson.Serialize(plain!),
            CustomerProvisioningCanonicalJson.Serialize(optedIn!));
        Assert.Null(plain!.CompanyReference);
    }

    [Fact]
    public void V5Payload_WithDuplicateCompanyReferenceKey_IsRejectedAsNoncanonical()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(SampleV5());
        var duplicated = json[..^1] + ",\"companyReference\":\"company-ref-0002\"}";

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            duplicated, Now, out var parsed, out var error, allowCompanyReference: true));
        Assert.Null(parsed);
        Assert.Equal("provisioning_payload_noncanonical", error);
    }

    [Fact]
    public void V5Payload_WithoutCompanyReferenceKey_IsRejected()
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(SampleV5());
        var stripped = json.Replace(",\"companyReference\":\"company-ref-0001\"", "", StringComparison.Ordinal);
        Assert.NotEqual(json, stripped);

        Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
            stripped, Now, out _, out _, allowCompanyReference: true));
    }

    [Fact]
    public void V5Payload_StillRequiresLicenseBindingAndEmail()
    {
        foreach (var invalid in new[]
        {
            SampleV5() with { LicenseId = null },
            SampleV5() with { Sequence = 0 },
            SampleV5() with { InitialAdminEmail = null }
        })
        {
            Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
                CustomerProvisioningCanonicalJson.Serialize(invalid), Now, out _, out _,
                allowCompanyReference: true));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(99)]
    public void UnknownSchemaVersion_IsRejectedEvenWhenOptedIn(int version)
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(SampleV5() with { SchemaVersion = version });

        foreach (var optIn in new[] { false, true })
        {
            Assert.False(CustomerProvisioningPackageValidator.TryValidatePayload(
                json, Now, out var parsed, out var error, allowCompanyReference: optIn));
            Assert.Null(parsed);
            Assert.Equal("provisioning_payload_invalid", error);
        }
    }

    [Fact]
    public void VersionConstants_KeepCurrentAtFourAndExposeV5Independently()
    {
        Assert.Equal(4, CustomerProvisioningPackageValidator.CurrentPayloadSchemaVersion);
        Assert.Equal(5, CustomerProvisioningPackageValidator.CompanyReferencePayloadSchemaVersion);
    }

    private static CustomerProvisioningPackage Sign(ECDsa key, CustomerProvisioningPayload payload)
    {
        var json = CustomerProvisioningCanonicalJson.Serialize(payload);
        var signature = key.SignData(
            System.Text.Encoding.UTF8.GetBytes(json),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new CustomerProvisioningPackage(
            1, "provisioning-key-1", LicenseAlgorithms.EcdsaP256Sha256, json, Convert.ToBase64String(signature));
    }

    private static CustomerProvisioningPayload Sample() => new(
        1,
        "9b0d2c4d-53bc-4bb2-a22c-90b30f2b58e8",
        "customer-ref-001",
        "installation-001",
        "Company Example",
        "independent",
        "initial-admin",
        new[] { "inventory", "finance" },
        Now.AddMinutes(-1),
        Now.AddHours(1),
        "abcdefghijklmnopqrstuvwxyz0123456789_-");

    private static CustomerProvisioningPayload SampleV2() => Sample() with
    {
        SchemaVersion = 2,
        FiscalPeriodTitle = "سال مالی ۱۴۰۵",
        FiscalPeriodStartDate = new DateOnly(2026, 1, 1),
        FiscalPeriodEndDate = new DateOnly(2027, 1, 1)
    };

    private static CustomerProvisioningPayload SampleV3() => SampleV2() with
    {
        SchemaVersion = 3,
        InitialAdminEmail = "admin@example.com"
    };

    private static CustomerProvisioningPayload SampleV4() => SampleV3() with
    {
        SchemaVersion = 4,
        LicenseId = "0a5f7d20-3a2c-4c8d-9e1f-2a6b7c8d9e10",
        Sequence = 7
    };

    private static CustomerProvisioningPayload SampleV5() => SampleV4() with
    {
        SchemaVersion = 5,
        CompanyReference = "company-ref-0001"
    };
}
