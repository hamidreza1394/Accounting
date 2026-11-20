using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Faranou.Vendor.Host.Signing;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class WindowsCngCertificatePolicyTests
{
    [Fact]
    public void StructurallyValidCertificatePasses()
    {
        using var certificate = CreateCertificate(
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(1));

        Assert.Null(WindowsCngCertificatePolicy.ValidateStructural(certificate, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ExpiredAndFutureCertificatesAreRejected()
    {
        using var expired = CreateCertificate(
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(-1));
        using var future = CreateCertificate(
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(2));
        var now = DateTimeOffset.UtcNow;

        Assert.Equal(
            "certificate_validity_window_invalid",
            WindowsCngCertificatePolicy.ValidateStructural(expired, now));
        Assert.Equal(
            "certificate_validity_window_invalid",
            WindowsCngCertificatePolicy.ValidateStructural(future, now));
    }

    [Fact]
    public void NonEcdsaCertificatesAreRejected()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Faranou Vendor RSA Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(1));

        Assert.Equal(
            "certificate_algorithm_unsupported",
            WindowsCngCertificatePolicy.ValidateStructural(certificate, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void NonP256CurvesAreRejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var request = new CertificateRequest("CN=Faranou Vendor P384 Test", key, HashAlgorithmName.SHA384);
        using var certificate = request.CreateSelfSigned(
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(1));

        Assert.Equal(
            "certificate_algorithm_unsupported",
            WindowsCngCertificatePolicy.ValidateStructural(certificate, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CertificateWithoutDigitalSignatureUsageIsRejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Faranou Vendor Encryption Only", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, critical: true));
        using var certificate = request.CreateSelfSigned(
            DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(1));

        Assert.Equal(
            "certificate_key_usage_invalid",
            WindowsCngCertificatePolicy.ValidateStructural(certificate, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PrivateKeysWithoutCngNonExportableAssuranceAreRejected()
    {
        using var certificate = CreateCertificate(
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(1));

        // An in-memory software key is not the D-107 MachineKey CNG key; it must never be accepted.
        Assert.NotNull(WindowsCngCertificatePolicy.ValidatePrivateKey(certificate));
    }

    [Fact]
    public void PublicKeyOnlyCertificatesHaveNoUsablePrivateKey()
    {
        using var certificate = CreateCertificate(
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(1));
        using var publicOnly = new X509Certificate2(
            certificate.Export(X509ContentType.Cert));

        Assert.Null(WindowsCngCertificatePolicy.ValidateStructural(publicOnly, DateTimeOffset.UtcNow));
        Assert.NotNull(WindowsCngCertificatePolicy.ValidatePrivateKey(publicOnly));
    }

    [Fact]
    public void CertificatesSharingPublicKeyAreDetectedEvenWithDifferentThumbprints()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var firstRequest = new CertificateRequest("CN=Faranou Vendor License", key, HashAlgorithmName.SHA256);
        firstRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        var secondRequest = new CertificateRequest("CN=Faranou Vendor Provisioning", key, HashAlgorithmName.SHA256);
        secondRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        using var first = firstRequest.CreateSelfSigned(
            DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(1));
        using var second = secondRequest.CreateSelfSigned(
            DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(1));
        using var independent = CreateCertificate(
            DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(1));

        Assert.NotEqual(first.Thumbprint, second.Thumbprint);
        Assert.True(WindowsCngCertificatePolicy.HaveSamePublicKey(first, second));
        Assert.False(WindowsCngCertificatePolicy.HaveSamePublicKey(first, independent));
    }

    private static X509Certificate2 CreateCertificate(DateTime notBefore, DateTime notAfter)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Faranou Vendor Test", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        return request.CreateSelfSigned(notBefore, notAfter);
    }
}
