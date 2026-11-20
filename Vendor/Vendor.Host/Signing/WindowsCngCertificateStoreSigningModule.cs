using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Faranou.Licensing.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faranou.Vendor.Host.Signing;

/// <summary>
/// Narrow signing boundary over the Windows Certificate Store. Purpose-specific certificates are
/// selected by thumbprint; the private key is only requested for a Sign operation and is never
/// exported, serialized or persisted. Signing requires an ECDSA P-256 key with CNG export policies
/// that forbid export (D-107 MachineKey requirement); a usable-key failure always fails closed.
/// </summary>
public interface IWindowsCngSigningModule
{
    ValueTask<LicenseSigningResult> SignAsync(
        WindowsCngSigningKeyOptions key,
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default);

    ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

public sealed class WindowsCngCertificateStoreSigningModule(
    IOptions<WindowsCngSigningOptions> options,
    IHostEnvironment environment,
    ILogger<WindowsCngCertificateStoreSigningModule> logger,
    TimeProvider? timeProvider = null) : IWindowsCngSigningModule
{
    private readonly WindowsCngSigningOptions _options = options.Value;
    private readonly IHostEnvironment _environment = environment;
    private readonly ILogger<WindowsCngCertificateStoreSigningModule> _logger = logger;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async ValueTask<LicenseSigningResult> SignAsync(
        WindowsCngSigningKeyOptions key,
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payloadCopy = canonicalPayload.ToArray();

        try
        {
            return await Task.Run(
                () => SignCore(key, payloadCopy),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            _logger.LogWarning(
                "Windows CNG signing provider is unavailable. KeyId={KeyId} FailureType={FailureType}.",
                key?.KeyId ?? "unknown",
                exception.GetType().Name);
            return LicenseSigningResult.Unavailable();
        }
    }

    public async ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await Task.Run(
                IsConfiguredPairUsable,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            _logger.LogWarning(
                "Windows CNG signing health probe failed. FailureType={FailureType}.",
                exception.GetType().Name);
            return false;
        }
    }

    private bool IsConfiguredPairUsable()
    {
        _options.Validate(_environment.IsProduction(), OperatingSystem.IsWindows());

        using var licenseCertificate = FindConfiguredCertificate(_options.LicenseKey);
        using var provisioningCertificate = FindConfiguredCertificate(_options.ProvisioningKey);
        if (licenseCertificate is null || provisioningCertificate is null)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        return WindowsCngCertificatePolicy.ValidateStructural(licenseCertificate, now) is null &&
               WindowsCngCertificatePolicy.ValidatePrivateKey(licenseCertificate, requireMachineKey: _environment.IsProduction()) is null &&
               WindowsCngCertificatePolicy.ValidateStructural(provisioningCertificate, now) is null &&
               WindowsCngCertificatePolicy.ValidatePrivateKey(provisioningCertificate, requireMachineKey: _environment.IsProduction()) is null &&
               !WindowsCngCertificatePolicy.HaveSamePublicKey(licenseCertificate, provisioningCertificate);
    }

    private LicenseSigningResult SignCore(WindowsCngSigningKeyOptions key, byte[] canonicalPayload)
    {
        _options.Validate(_environment.IsProduction(), OperatingSystem.IsWindows());
        ValidateRequestedKey(key);
        if (!IsConfiguredPairUsable())
        {
            return LicenseSigningResult.Unavailable();
        }

        var certificate = FindConfiguredCertificate(key);
        if (certificate is null)
        {
            return LicenseSigningResult.Unavailable();
        }

        try
        {
            var now = _timeProvider.GetUtcNow();
            var structuralError = WindowsCngCertificatePolicy.ValidateStructural(certificate, now);
            if (structuralError is not null)
            {
                _logger.LogWarning(
                    "Windows CNG signing certificate is not usable. KeyId={KeyId} Reason={Reason}.",
                    key.KeyId,
                    structuralError);
                return LicenseSigningResult.Unavailable();
            }

            var keyError = WindowsCngCertificatePolicy.ValidatePrivateKey(certificate);
            if (keyError is not null)
            {
                _logger.LogWarning(
                    "Windows CNG signing private key is not acceptable. KeyId={KeyId} Reason={Reason}.",
                    key.KeyId,
                    keyError);
                return LicenseSigningResult.Unavailable();
            }

            using var ecdsa = certificate.GetECDsaPrivateKey();
            if (ecdsa is null)
            {
                return LicenseSigningResult.Unavailable();
            }

            var signature = ecdsa.SignData(
                canonicalPayload,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            if (signature.Length != 64)
            {
                return LicenseSigningResult.Unavailable();
            }

            return LicenseSigningResult.Success(
                key.KeyId,
                LicenseAlgorithms.EcdsaP256Sha256,
                signature);
        }
        finally
        {
            certificate.Dispose();
        }
    }

    /// <summary>Finds exactly one certificate for the purpose. Anything ambiguous fails closed.</summary>
    private X509Certificate2? FindConfiguredCertificate(WindowsCngSigningKeyOptions key)
    {
        if (!Enum.TryParse(_options.StoreLocation, ignoreCase: true, out StoreLocation storeLocation))
        {
            return null;
        }

        using var store = new X509Store(_options.StoreName, storeLocation);
        try
        {
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        }
        catch (CryptographicException exception)
        {
            _logger.LogWarning(
                "Windows CNG certificate store is unavailable. StoreLocation={StoreLocation} Reason={Reason}.",
                storeLocation,
                exception.GetType().Name);
            return null;
        }

        return WindowsCngCertificateLookup.TakeExactlyOne(
            store.Certificates,
            _options.ThumbprintNormalised(key));
    }

    private void ValidateRequestedKey(WindowsCngSigningKeyOptions key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!string.Equals(key.KeyId, _options.LicenseKey.KeyId, StringComparison.Ordinal) &&
            !string.Equals(key.KeyId, _options.ProvisioningKey.KeyId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Windows CNG signing KeyId is not configured for an approved Vendor signing purpose.");
        }
    }
}

/// <summary>
/// Shared certificate policy for the D-107 signer. Structural checks run everywhere; the private
/// key check requires a CNG key whose export policy forbids any export (MachineKey semantics).
/// </summary>
public static class WindowsCngCertificatePolicy
{
    /// <summary>Structural, platform-neutral certificate usability rules.</summary>
    public static string? ValidateStructural(X509Certificate2 certificate, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (certificate.NotBefore > nowUtc || certificate.NotAfter < nowUtc)
        {
            return "certificate_validity_window_invalid";
        }

        using var publicKey = certificate.PublicKey.GetECDsaPublicKey();
        if (publicKey is null || publicKey.KeySize != 256 ||
            !string.Equals(
                publicKey.ExportParameters(false).Curve.Oid.Value,
                ECCurve.NamedCurves.nistP256.Oid.Value,
                StringComparison.Ordinal))
        {
            return "certificate_algorithm_unsupported";
        }

        var keyUsageExtensions = certificate.Extensions.OfType<X509KeyUsageExtension>().ToArray();
        if (keyUsageExtensions.Length != 1 ||
            !keyUsageExtensions[0].KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
        {
            return "certificate_key_usage_invalid";
        }

        return null;
    }

    /// <summary>Compares public SPKI only; private key material is never accessed.</summary>
    public static bool HaveSamePublicKey(X509Certificate2 first, X509Certificate2 second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        using var firstKey = first.GetECDsaPublicKey();
        using var secondKey = second.GetECDsaPublicKey();
        if (firstKey is null || secondKey is null)
        {
            return false;
        }

        var firstSpki = firstKey.ExportSubjectPublicKeyInfo();
        var secondSpki = secondKey.ExportSubjectPublicKeyInfo();
        return firstSpki.Length == secondSpki.Length &&
               CryptographicOperations.FixedTimeEquals(firstSpki, secondSpki);
    }

    /// <summary>Rejects non-CNG or exportable private keys; private key material is never read.</summary>
    public static string? ValidatePrivateKey(X509Certificate2 certificate, bool requireMachineKey = false)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (!OperatingSystem.IsWindows())
        {
            return "certificate_key_not_cng_non_exportable";
        }

        using var ecdsa = certificate.GetECDsaPrivateKey();
        if (ecdsa is not ECDsaCng cngEcdsa)
        {
            return "certificate_key_not_cng_non_exportable";
        }

        if (!cngEcdsa.Key.Provider.Equals(CngProvider.MicrosoftSoftwareKeyStorageProvider))
        {
            return "certificate_key_provider_unsupported";
        }

        if (cngEcdsa.Key.ExportPolicy != CngExportPolicies.None)
        {
            return "certificate_key_exportable";
        }

        if (requireMachineKey && !cngEcdsa.Key.IsMachineKey)
        {
            return "certificate_key_not_machine_key";
        }

        return null;
    }
}

/// <summary>
/// Selects exactly one certificate while disposing every certificate wrapper returned by the store
/// except the selected wrapper, whose ownership transfers to the caller.
/// </summary>
internal static class WindowsCngCertificateLookup
{
    public static X509Certificate2? TakeExactlyOne(
        X509Certificate2Collection certificates,
        string thumbprint)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);

        X509Certificate2? selected = null;
        var matchCount = 0;
        foreach (var certificate in certificates)
        {
            if (string.Equals(certificate.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
                if (matchCount == 1)
                {
                    selected = certificate;
                }
                else
                {
                    certificate.Dispose();
                }
            }
            else
            {
                certificate.Dispose();
            }
        }

        if (matchCount == 1)
        {
            return selected;
        }

        selected?.Dispose();
        return null;
    }
}
