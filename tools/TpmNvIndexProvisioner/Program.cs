using System.Globalization;
using System.Security.Cryptography;
using Tpm2Lib;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("This provisioning utility currently supports Windows TBS only.");
    return 2;
}

if (args.Length != 1 ||
    !TryParseOffset(args[0], out var indexOffset) ||
    indexOffset < 0x1000 ||
    indexOffset > 0x00FFFFFF)
{
    Console.Error.WriteLine("Usage: TpmNvIndexProvisioner <NV-index-offset-decimal-or-0xHEX>");
    return 2;
}

byte[] platformAuth = [];
byte[] nvAuth = [];
AuthValue? platformAuthValue = null;
AuthValue? nvAuthValue = null;
TpmHandle? runtimeIndex = null;
Tpm2Device? device = null;
Tpm2? tpm = null;

try
{
    var platformAuthText = Environment.GetEnvironmentVariable("FARANOU_TPM_PLATFORM_AUTH_BASE64");
    var nvAuthText = Environment.GetEnvironmentVariable("FARANOU_TPM_NV_AUTH_BASE64");
    platformAuth = string.IsNullOrWhiteSpace(platformAuthText)
        ? []
        : System.Convert.FromBase64String(platformAuthText);
    nvAuth = string.IsNullOrWhiteSpace(nvAuthText)
        ? throw new InvalidOperationException("FARANOU_TPM_NV_AUTH_BASE64 is required.")
        : System.Convert.FromBase64String(nvAuthText);

    if (platformAuth.Length > 32 || nvAuth.Length is < 1 or > 32)
        throw new InvalidOperationException("TPM authorization length is invalid.");

    platformAuthValue = new AuthValue(platformAuth);
    nvAuthValue = new AuthValue(nvAuth);

    device = new TbsDevice();
    device.Connect();
    tpm = new Tpm2(device);

    var nvHandle = TpmHandle.NV(indexOffset);
    var attributes = (NvAttr)(0x00000040u | 0x00000004u | 0x00040000u);
    var publicArea = new NvPublic(nvHandle, TpmAlgId.Sha256, attributes, null, 32);

    // The TPM operation is intentionally create-only. There is no undefine/reset path.
    tpm[platformAuthValue].NvDefineSpace(TpmRh.Platform, nvAuthValue, publicArea);

    var readBackPublic = tpm.NvReadPublic(nvHandle, out _);
    var readBackAttributes = (uint)readBackPublic.attributes;
    if (readBackPublic.nameAlg != TpmAlgId.Sha256 ||
        readBackPublic.dataSize != 32 ||
        (readBackAttributes & 0x000000F0u) != 0x00000040u ||
        (readBackAttributes & 0x00000004u) == 0 ||
        (readBackAttributes & 0x00040000u) == 0 ||
        (readBackAttributes & 0x40000000u) == 0)
    {
        throw new InvalidOperationException("The provisioned TPM NV index does not meet the required public attributes.");
    }

    runtimeIndex = TpmHandle.NV(indexOffset);
    runtimeIndex.SetAuth(nvAuthValue);
    var initialValue = tpm.NvRead(runtimeIndex, runtimeIndex, 32, 0);
    if (initialValue.Length != 32 || initialValue.Any(value => value != 0))
    {
        throw new InvalidOperationException("The newly provisioned TPM NV Extend index is not initialized to 32 zero bytes.");
    }

    Console.WriteLine($"Provisioned TPM 2.0 NV Extend index offset 0x{indexOffset:X}.");
    Console.WriteLine("Verified: SHA-256, 32 bytes, AuthRead, AuthWrite, PlatformCreate, initial value is zero.");
    Console.WriteLine("Record the TPM identity, index offset and policy evidence. Do not record authorization values.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"TPM NV index provisioning failed: {exception.GetType().Name}");
    return 1;
}
finally
{
    if (runtimeIndex?.Auth is { Length: > 0 } runtimeAuth)
        CryptographicOperations.ZeroMemory(runtimeAuth);
    if (platformAuthValue is not null)
        CryptographicOperations.ZeroMemory(platformAuthValue.AuthVal);
    if (nvAuthValue is not null)
        CryptographicOperations.ZeroMemory(nvAuthValue.AuthVal);
    CryptographicOperations.ZeroMemory(platformAuth);
    CryptographicOperations.ZeroMemory(nvAuth);
    tpm?.Dispose();
    device?.Dispose();
}

static bool TryParseOffset(string value, out uint offset)
{
    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        return uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset);

    return uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out offset);
}
