using System.Security.Cryptography;
using System.Text;
using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

public sealed class CustomerProvisioningAcknowledgementValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private const string RequestId = "request-001";
    private const string Nonce = "nonce-0123456789";
    private const string PayloadHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string CustomerReference = "customer-001";
    private const string InstallationId = "installation-001";

    [Fact]
    public void ValidSignedAcknowledgement_VerifiesSignatureAndAllRequestBindings()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample());

        var valid = CustomerProvisioningAcknowledgementValidator.TryValidateAndVerify(
            envelope, key.ExportSubjectPublicKeyInfo(), RequestId, Nonce, PayloadHash,
            CustomerReference, InstallationId, Now, out var parsed, out var error);

        Assert.True(valid, error);
        Assert.NotNull(parsed);
        Assert.Equal("Applied", parsed!.Result);
    }

    [Fact]
    public void TamperedAcknowledgement_IsRejectedBySignatureVerification()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample()) with
        {
            AcknowledgementJson = "not-json"
        };

        Assert.False(CustomerProvisioningAcknowledgementValidator.TryValidateAndVerify(
            envelope, key.ExportSubjectPublicKeyInfo(), RequestId, Nonce, PayloadHash,
            CustomerReference, InstallationId, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_acknowledgement_signature_invalid", error);
    }

    [Fact]
    public void ValidSignatureWithWrongNonce_IsRejectedAsBindingMismatch()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample() with { Nonce = "another-request-nonce" });

        Assert.False(CustomerProvisioningAcknowledgementValidator.TryValidateAndVerify(
            envelope, key.ExportSubjectPublicKeyInfo(), RequestId, Nonce, PayloadHash,
            CustomerReference, InstallationId, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_acknowledgement_binding_mismatch", error);
    }

    [Fact]
    public void RejectedAcknowledgementRequiresBoundedErrorCode()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample() with { Result = "Rejected", ErrorCode = null });

        Assert.False(CustomerProvisioningAcknowledgementValidator.TryValidateAndVerify(
            envelope, key.ExportSubjectPublicKeyInfo(), RequestId, Nonce, PayloadHash,
            CustomerReference, InstallationId, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_acknowledgement_payload_invalid", error);
    }

    [Fact]
    public void UnknownResult_IsRejectedEvenWhenSignedByTrustedKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample() with { Result = "Confirmed" });

        Assert.False(CustomerProvisioningAcknowledgementValidator.TryValidateAndVerify(
            envelope, key.ExportSubjectPublicKeyInfo(), RequestId, Nonce, PayloadHash,
            CustomerReference, InstallationId, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_acknowledgement_payload_invalid", error);
    }

    [Fact]
    public void PayloadHashMismatch_IsRejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample() with
        {
            PayloadSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        });

        Assert.False(CustomerProvisioningAcknowledgementValidator.TryValidateAndVerify(
            envelope, key.ExportSubjectPublicKeyInfo(), RequestId, Nonce, PayloadHash,
            CustomerReference, InstallationId, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_acknowledgement_binding_mismatch", error);
    }

    [Fact]
    public void FutureDatedAcknowledgement_IsRejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = Sign(key, Sample() with { ProcessedAtUtc = Now.AddMinutes(3) });

        Assert.False(CustomerProvisioningAcknowledgementValidator.TryValidateAndVerify(
            envelope, key.ExportSubjectPublicKeyInfo(), RequestId, Nonce, PayloadHash,
            CustomerReference, InstallationId, Now, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.Equal("provisioning_acknowledgement_payload_invalid", error);
    }

    private static CustomerProvisioningAcknowledgementEnvelope Sign(
        ECDsa key, CustomerProvisioningAcknowledgement acknowledgement)
    {
        var json = CustomerProvisioningAcknowledgementValidator.Serialize(acknowledgement);
        var signature = key.SignData(Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new CustomerProvisioningAcknowledgementEnvelope(
            1, "customer-ack-key-001", LicenseAlgorithms.EcdsaP256Sha256,
            json, Convert.ToBase64String(signature));
    }

    private static CustomerProvisioningAcknowledgement Sample() => new(
        1, RequestId, Nonce, PayloadHash, CustomerReference, InstallationId,
        "Applied", Now.AddMinutes(-1), null);
}
