using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Faranou.Licensing.Contracts;
using Faranou.Vendor.Persistence.Auditing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;

namespace Faranou.Vendor.Host.Signing;

/// <summary>Configuration for one PKCS#11 module and two purpose-separated signing keys.</summary>
public sealed class Pkcs11SigningOptions
{
    private static readonly Regex VersionedKeyIdPattern = new(
        "^[a-z0-9][a-z0-9-]{0,49}-v[1-9][0-9]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string? ModulePath { get; set; }
    public string? TokenLabel { get; set; }
    public string? ApprovedTokenManufacturer { get; set; }
    public string? ApprovedTokenModel { get; set; }
    public string? UserPin { get; set; }
    public Pkcs11SigningKeyOptions LicenseKey { get; set; } = new();
    public Pkcs11SigningKeyOptions ProvisioningKey { get; set; } = new();

    /// <summary>
    /// Validates non-secret configuration without exposing PIN values in exceptions or logs.
    /// </summary>
    public void Validate()
    {
        if (LicenseKey is null || ProvisioningKey is null)
        {
            throw new InvalidOperationException("Separate license and provisioning signing key identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(ModulePath) || !Path.IsPathFullyQualified(ModulePath))
        {
            throw new InvalidOperationException("PKCS#11 module path must be an absolute path.");
        }

        if (string.IsNullOrWhiteSpace(TokenLabel) || Encoding.UTF8.GetByteCount(TokenLabel) > 32)
        {
            throw new InvalidOperationException("PKCS#11 token label is missing or invalid.");
        }

        if (string.IsNullOrWhiteSpace(UserPin))
        {
            throw new InvalidOperationException("PKCS#11 user PIN is not configured.");
        }

        ValidateKeyId(LicenseKey?.KeyId);
        ValidateKeyId(ProvisioningKey?.KeyId);

        if (string.Equals(LicenseKey.KeyId, ProvisioningKey.KeyId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("License and provisioning signing keys must have distinct versioned KeyIds.");
        }
    }

    private static void ValidateKeyId(string? keyId)
    {
        if (keyId is null || keyId.Length > 60 || !VersionedKeyIdPattern.IsMatch(keyId))
        {
            throw new InvalidOperationException("PKCS#11 signing KeyId must be a unique, versioned lower-case identifier.");
        }
    }
}

/// <summary>Identifier of a private key object created during the controlled HSM Key Ceremony.</summary>
public sealed class Pkcs11SigningKeyOptions
{
    public string KeyId { get; set; } = string.Empty;
}

/// <summary>
/// Narrow boundary around the PKCS#11 module. The two Vendor signing interfaces receive distinct
/// key options and cannot silently share a key identifier.
/// </summary>
public interface IPkcs11SigningModule
{
    ValueTask<LicenseSigningResult> SignAsync(
        Pkcs11SigningKeyOptions key,
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default);

    ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// PKCS#11 ECDSA P-256 implementation. Private keys are looked up as non-extractable token objects;
/// this class never creates, exports, serializes, or persists private key material.
/// </summary>
public sealed class Pkcs11SigningModule : IPkcs11SigningModule, IDisposable
{
    private static readonly byte[] P256EcParameters =
    [
        0x06, 0x08, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07
    ];

    private static readonly CKA[] PrivateKeyAttributes =
    [
        CKA.CKA_SENSITIVE,
        CKA.CKA_EXTRACTABLE,
        CKA.CKA_ALWAYS_SENSITIVE,
        CKA.CKA_NEVER_EXTRACTABLE,
        CKA.CKA_SIGN,
        CKA.CKA_EC_PARAMS
    ];

    private readonly Pkcs11SigningOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<Pkcs11SigningModule> _logger;
    private readonly Lazy<IPkcs11Library> _library;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public Pkcs11SigningModule(
        IOptions<Pkcs11SigningOptions> options,
        IHostEnvironment environment,
        ILogger<Pkcs11SigningModule> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
        _library = new Lazy<IPkcs11Library>(LoadLibrary, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async ValueTask<LicenseSigningResult> SignAsync(
        Pkcs11SigningKeyOptions key,
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payloadCopy = canonicalPayload.ToArray();

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        catch (Pkcs11Exception exception)
        {
            _logger.LogWarning(
                "PKCS#11 signing failed. FailureCode={FailureCode}.",
                exception.RV.ToString());
            return LicenseSigningResult.Unavailable();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            _logger.LogWarning(
                "PKCS#11 signing provider is unavailable. FailureType={FailureType}.",
                exception.GetType().Name);
            return LicenseSigningResult.Unavailable();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(CheckAvailableCore, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            _logger.LogWarning(
                "PKCS#11 signing health probe failed. FailureType={FailureType}.",
                exception.GetType().Name);
            return false;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private IPkcs11Library LoadLibrary()
    {
        _options.Validate();
        var factories = new Pkcs11InteropFactories();
        return factories.Pkcs11LibraryFactory.LoadPkcs11Library(
            factories,
            _options.ModulePath!,
            AppType.MultiThreaded);
    }

    private LicenseSigningResult SignCore(Pkcs11SigningKeyOptions key, byte[] canonicalPayload)
    {
        _options.Validate();
        ValidateRequestedKey(key);

        var slot = FindConfiguredSlot();
        if (slot is null || !SupportsEcdsa(slot))
        {
            return LicenseSigningResult.Unavailable();
        }

        using var session = slot.OpenSession(SessionType.ReadOnly);
        var loggedIn = false;
        try
        {
            session.Login(CKU.CKU_USER, _options.UserPin!);
            loggedIn = true;

            var privateKey = FindAndValidatePrivateKey(session, key);
            if (privateKey is null)
            {
                return LicenseSigningResult.Unavailable();
            }

            // CKM_ECDSA accepts a digest, not the original message. Hash exactly once with SHA-256.
            var digest = SHA256.HashData(canonicalPayload);
            using var mechanism = session.Factories.MechanismFactory.Create(CKM.CKM_ECDSA);
            var signature = session.Sign(mechanism, privateKey, digest);
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
            if (loggedIn)
            {
                session.Logout();
            }
        }
    }

    private bool CheckAvailableCore()
    {
        _options.Validate();
        var slot = FindConfiguredSlot();
        if (slot is null || !SupportsEcdsa(slot))
        {
            return false;
        }

        using var session = slot.OpenSession(SessionType.ReadOnly);
        var loggedIn = false;
        try
        {
            session.Login(CKU.CKU_USER, _options.UserPin!);
            loggedIn = true;
            return FindAndValidatePrivateKey(session, _options.LicenseKey) is not null &&
                   FindAndValidatePrivateKey(session, _options.ProvisioningKey) is not null;
        }
        finally
        {
            if (loggedIn)
            {
                session.Logout();
            }
        }
    }

    private ISlot? FindConfiguredSlot()
    {
        var matchingSlots = new List<ISlot>();
        foreach (var slot in _library.Value.GetSlotList(SlotsType.WithTokenPresent))
        {
            var tokenInfo = slot.GetTokenInfo();
            if (string.Equals(tokenInfo.Label.Trim(), _options.TokenLabel, StringComparison.Ordinal))
            {
                matchingSlots.Add(slot);
            }
        }

        if (matchingSlots.Count != 1)
        {
            return null;
        }

        var selected = matchingSlots[0];
        if (_environment.IsEnvironment("Testing"))
        {
            return selected;
        }

        // Production/Staging accept only an explicitly allowlisted hardware token, never a soft token.
        var slotInfo = selected.GetSlotInfo();
        var selectedTokenInfo = selected.GetTokenInfo();
        if (!slotInfo.SlotFlags.HardwareSlot ||
            string.IsNullOrWhiteSpace(_options.ApprovedTokenManufacturer) ||
            string.IsNullOrWhiteSpace(_options.ApprovedTokenModel) ||
            !string.Equals(
                selectedTokenInfo.ManufacturerId.Trim(),
                _options.ApprovedTokenManufacturer,
                StringComparison.Ordinal) ||
            !string.Equals(
                selectedTokenInfo.Model.Trim(),
                _options.ApprovedTokenModel,
                StringComparison.Ordinal))
        {
            return null;
        }

        return selected;
    }

    private static bool SupportsEcdsa(ISlot slot) =>
        slot.GetMechanismList().Contains(CKM.CKM_ECDSA);

    private static IObjectHandle? FindAndValidatePrivateKey(Net.Pkcs11Interop.HighLevelAPI.ISession session, Pkcs11SigningKeyOptions key)
    {
        var keyId = Encoding.ASCII.GetBytes(key.KeyId);
        var searchTemplate = new List<IObjectAttribute>
        {
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY),
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_KEY_TYPE, CKK.CKK_EC),
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_LABEL, key.KeyId),
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, keyId),
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_SIGN, true)
        };

        List<IObjectHandle> matches;
        try
        {
            matches = session.FindAllObjects(searchTemplate);
        }
        finally
        {
            foreach (var attribute in searchTemplate)
            {
                attribute.Dispose();
            }
        }

        if (matches.Count != 1)
        {
            return null;
        }

        var privateKey = matches[0];
        var attributes = session.GetAttributeValue(privateKey, PrivateKeyAttributes.ToList());
        try
        {
            if (attributes.Count != PrivateKeyAttributes.Length || attributes.Any(attribute => attribute.CannotBeRead))
            {
                return null;
            }

            var isSensitive = attributes[0].GetValueAsBool();
            var isExtractable = attributes[1].GetValueAsBool();
            var alwaysSensitive = attributes[2].GetValueAsBool();
            var neverExtractable = attributes[3].GetValueAsBool();
            var canSign = attributes[4].GetValueAsBool();
            var ecParameters = attributes[5].GetValueAsByteArray();

            if (!isSensitive || isExtractable || !alwaysSensitive || !neverExtractable || !canSign ||
                !CryptographicOperations.FixedTimeEquals(ecParameters, P256EcParameters))
            {
                return null;
            }

            return privateKey;
        }
        finally
        {
            foreach (var attribute in attributes)
            {
                attribute.Dispose();
            }
        }
    }

    private void ValidateRequestedKey(Pkcs11SigningKeyOptions key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!string.Equals(key.KeyId, _options.LicenseKey.KeyId, StringComparison.Ordinal) &&
            !string.Equals(key.KeyId, _options.ProvisioningKey.KeyId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PKCS#11 signing KeyId is not configured for an approved Vendor signing purpose.");
        }
    }

    public void Dispose()
    {
        if (_library.IsValueCreated)
        {
            _library.Value.Dispose();
        }
        _operationGate.Dispose();
    }
}

/// <summary>Shared metadata-only audit wrapper for the two purpose-specific signing providers.</summary>
public abstract class Pkcs11SigningProviderBase
{
    private readonly IPkcs11SigningModule _module;
    private readonly IVendorAuditWriter _auditWriter;
    private readonly ILogger _logger;
    private readonly string _purpose;

    protected Pkcs11SigningProviderBase(
        IPkcs11SigningModule module,
        IVendorAuditWriter auditWriter,
        ILogger logger,
        string purpose)
    {
        _module = module;
        _auditWriter = auditWriter;
        _logger = logger;
        _purpose = purpose;
    }

    protected async ValueTask<LicenseSigningResult> SignAsync(
        Pkcs11SigningKeyOptions key,
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken)
    {
        var result = await _module.SignAsync(key, canonicalPayload, cancellationToken).ConfigureAwait(false);
        var outcome = result.Succeeded ? "succeeded" : "failed";

        try
        {
            await _auditWriter.AppendAsync(
                new VendorAuditWriteRequest(
                    null,
                    $"vendor.signing.{_purpose}.{key.KeyId}.{outcome}",
                    "signing_operation",
                    null,
                    Activity.Current?.TraceId.ToString()),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            // A signature is never returned to the caller if its audit event cannot be persisted.
            _logger.LogCritical(
                "Vendor signing audit append failed; signature result was discarded. Purpose={Purpose} FailureType={FailureType}.",
                _purpose,
                exception.GetType().Name);
            return LicenseSigningResult.Unavailable();
        }

        if (result.Succeeded)
        {
            _logger.LogInformation(
                "Vendor signing operation completed. Purpose={Purpose} KeyId={KeyId} Result={Result}.",
                _purpose,
                key.KeyId,
                outcome);
        }
        else
        {
            _logger.LogWarning(
                "Vendor signing operation did not complete. Purpose={Purpose} KeyId={KeyId} Result={Result}.",
                _purpose,
                key.KeyId,
                outcome);
        }

        return result;
    }
}

/// <summary>PKCS#11-backed Vendor license signer using its license-only KeyId.</summary>
public sealed class Pkcs11LicenseSigningProvider : Pkcs11SigningProviderBase, ILicenseSigningProvider
{
    private readonly Pkcs11SigningKeyOptions _key;

    public Pkcs11LicenseSigningProvider(
        IPkcs11SigningModule module,
        IOptions<Pkcs11SigningOptions> options,
        IVendorAuditWriter auditWriter,
        ILogger<Pkcs11LicenseSigningProvider> logger)
        : base(module, auditWriter, logger, "license")
    {
        _key = options.Value.LicenseKey;
    }

    public ValueTask<LicenseSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default) =>
        base.SignAsync(_key, canonicalPayload, cancellationToken);
}

/// <summary>PKCS#11-backed Vendor provisioning-package signer using its package-only KeyId.</summary>
public sealed class Pkcs11ProvisioningSigningProvider : Pkcs11SigningProviderBase, IProvisioningSigningProvider
{
    private readonly Pkcs11SigningKeyOptions _key;

    public Pkcs11ProvisioningSigningProvider(
        IPkcs11SigningModule module,
        IOptions<Pkcs11SigningOptions> options,
        IVendorAuditWriter auditWriter,
        ILogger<Pkcs11ProvisioningSigningProvider> logger)
        : base(module, auditWriter, logger, "provisioning")
    {
        _key = options.Value.ProvisioningKey;
    }

    public ValueTask<LicenseSigningResult> SignAsync(
        ReadOnlyMemory<byte> canonicalPayload,
        CancellationToken cancellationToken = default) =>
        base.SignAsync(_key, canonicalPayload, cancellationToken);
}

/// <summary>Readiness probe for the configured PKCS#11 module and both purpose-separated keys.</summary>
public sealed class Pkcs11SigningHealthCheck(IPkcs11SigningModule module) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var available = await module.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        return available
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("PKCS#11 signing provider is unavailable.");
    }
}
