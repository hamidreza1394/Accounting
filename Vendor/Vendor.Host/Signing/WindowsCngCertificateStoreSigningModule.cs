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
                () => FindConfiguredCertificate(_options.LicenseKey) is not null &&
                     FindConfiguredCertificate(_options.ProvisioningKey) is not null,
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

    private LicenseSigningResult SignCore(WindowsCngSigningKeyOptions key, byte[] canonicalPayload)
    {
        _options.Validate(_environment.IsProduction(), OperatingSystem.IsWindows());
        ValidateRequestedKey(key);

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

        var matches = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            _options.ThumbprintNormalised(key),
            validOnly: false);
        if (matches.Count != 1)
        {
            return null;
        }

        return matches[0];
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

        return null;
    }

    /// <summary>Rejects non-CNG or exportable private keys; private key material is never read.</summary>
    public static string? ValidatePrivateKey(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        using var ecdsa = certificate.GetECDsaPrivateKey();
        if (ecdsa is not ECDsaCng cngEcdsa)
        {
            return "certificate_key_not_cng_non_exportable";
        }

        const CngExportPolicies forbiddenExportPolicies =
            CngExportPolicies.AllowPlaintextExport |
            CngExportPolicies.AllowArchiving |
            CngExportPolicies.AllowPlaintextArchiving;
        if ((cngEcdsa.Key.ExportPolicy & forbiddenExportPolicies) != 0)
        {
            return "certificate_key_exportable";
        }

        return null;
    }
}
