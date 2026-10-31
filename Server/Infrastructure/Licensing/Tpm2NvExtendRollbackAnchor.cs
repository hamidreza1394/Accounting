using System.Buffers.Binary;
using System.Security.Cryptography;
using Faranou.Licensing.Contracts;
using Microsoft.Extensions.Options;
using Tpm2Lib;

namespace Infrastructure.Licensing;

public sealed class TpmNvExtendOptions
{
    public uint NvIndexOffset { get; set; }
    public string? NvIndexAuthValueBase64 { get; set; }
    public string LinuxDevicePath { get; set; } = "/dev/tpmrm0";
}

/// <summary>
/// TPM 2.0 NV Extend adapter using Microsoft TSS.MSR.
/// Windows uses TBS; Linux uses a strict local device adapter that never falls back to a simulator.
/// The NV index must be provisioned out-of-band by the approved TPM ceremony.
/// </summary>
public sealed class Tpm2NvExtendRollbackAnchor : IOfflineRevocationRollbackAnchor
{
    private const uint NvTypeMask = 0x000000F0;
    private const uint NvTypeExtend = 0x00000040;
    private const uint NvAuthWrite = 0x00000004;
    private const uint NvAuthRead = 0x00040000;
    private const uint NvPlatformCreate = 0x40000000;
    private readonly TpmNvExtendOptions _options;

    public Tpm2NvExtendRollbackAnchor(IOptions<TpmNvExtendOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (_options.NvIndexOffset < 0x1000 || _options.NvIndexOffset > 0x00FFFFFF)
        {
            throw new InvalidOperationException("The TPM NV index offset is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.NvIndexAuthValueBase64))
        {
            throw new InvalidOperationException("The TPM NV index authorization is not configured.");
        }

        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("TPM 2.0 NV Extend is supported only on Windows and Linux.");
        }

        if (OperatingSystem.IsLinux() &&
            _options.LinuxDevicePath is not ("/dev/tpmrm0" or "/dev/tpm0"))
        {
            throw new InvalidOperationException("Only the local Linux TPM device nodes are supported.");
        }
    }

    public Task<string> ReadDigestAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteTpm(ReadDigest);
        }, cancellationToken);

    public Task<string> ExtendAsync(
        string expectedCurrentDigest,
        ReadOnlyMemory<byte> extensionData,
        string expectedResultDigest,
        CancellationToken cancellationToken = default)
    {
        if (!IsDigest(expectedCurrentDigest) ||
            !IsDigest(expectedResultDigest) ||
            extensionData.IsEmpty ||
            extensionData.Length > 1024)
        {
            throw new OfflineRevocationRollbackAnchorUnavailableException();
        }

        var data = extensionData.ToArray();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteTpm(tpm =>
            {
                var index = CreateAuthorizedIndexHandle();
                try
                {
                    var current = ReadDigest(tpm, index);
                    if (!string.Equals(current, expectedCurrentDigest, StringComparison.Ordinal))
                    {
                        throw new OfflineRevocationRollbackAnchorUnavailableException();
                    }

                    tpm.NvExtend(index, index, data);
                    var actual = ReadDigest(tpm, index);
                    if (!string.Equals(actual, expectedResultDigest, StringComparison.Ordinal))
                    {
                        throw new OfflineRevocationRollbackAnchorUnavailableException();
                    }

                    return actual;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(index.Auth);
                }
            });
        }, cancellationToken);
    }

    private string ReadDigest(Tpm2 tpm)
    {
        var index = CreateAuthorizedIndexHandle();
        try
        {
            return ReadDigest(tpm, index);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(index.Auth);
        }
    }

    private string ReadDigest(Tpm2 tpm, TpmHandle index)
    {
        var publicArea = tpm.NvReadPublic(index, out _);
        var attributes = (uint)publicArea.attributes;
        if (publicArea.nameAlg != TpmAlgId.Sha256 ||
            publicArea.dataSize != 32 ||
            (attributes & NvTypeMask) != NvTypeExtend ||
            (attributes & NvAuthRead) == 0 ||
            (attributes & NvAuthWrite) == 0 ||
            (attributes & NvPlatformCreate) == 0)
        {
            throw new OfflineRevocationRollbackAnchorUnavailableException();
        }

        var value = tpm.NvRead(index, index, 32, 0);
        if (value.Length != 32)
        {
            throw new OfflineRevocationRollbackAnchorUnavailableException();
        }

        return Convert.ToHexString(value).ToLowerInvariant();
    }

    private TpmHandle CreateAuthorizedIndexHandle()
    {
        byte[] auth;
        try
        {
            auth = Convert.FromBase64String(_options.NvIndexAuthValueBase64!);
        }
        catch (FormatException exception)
        {
            throw new OfflineRevocationRollbackAnchorUnavailableException(exception);
        }

        if (auth.Length is < 1 or > 32)
        {
            CryptographicOperations.ZeroMemory(auth);
            throw new OfflineRevocationRollbackAnchorUnavailableException();
        }

        var handle = TpmHandle.NV(_options.NvIndexOffset);
        handle.SetAuth(new AuthValue(auth));
        CryptographicOperations.ZeroMemory(auth);
        return handle;
    }

    private TResult ExecuteTpm<TResult>(Func<Tpm2, TResult> operation)
    {
        Tpm2Device? device = null;
        Tpm2? tpm = null;
        try
        {
            device = OperatingSystem.IsWindows()
                ? new TbsDevice()
                : new StrictLinuxTpmDevice(_options.LinuxDevicePath);
            device.Connect();
            tpm = new Tpm2(device);
            return operation(tpm);
        }
        catch (OfflineRevocationRollbackAnchorUnavailableException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new OfflineRevocationRollbackAnchorUnavailableException(exception);
        }
        finally
        {
            try
            {
                tpm?.Dispose();
            }
            finally
            {
                device?.Dispose();
            }
        }
    }

    private static bool IsDigest(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 64)
            return false;

        try
        {
            return Convert.FromHexString(value).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Direct Linux TPM character-device transport. Deliberately does not use TSS.MSR's
    /// LinuxTpmDevice because that implementation may fall back to a localhost simulator.
    /// </summary>
    private sealed class StrictLinuxTpmDevice(string path) : Tpm2Device
    {
        private FileStream? _stream;

        public override void Connect()
        {
            if (path is not ("/dev/tpmrm0" or "/dev/tpm0"))
                throw new PlatformNotSupportedException("Only local Linux TPM device nodes are supported.");

            _stream ??= new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            _HasRM = path == "/dev/tpmrm0";
        }

        public override void DispatchCommand(CommandModifier modifier, byte[] command, out byte[] response)
        {
            var stream = _stream ?? throw new IOException("The Linux TPM device is not connected.");
            stream.Write(command, 0, command.Length);
            stream.Flush();

            var buffer = new byte[8192];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead < 10)
                throw new IOException("The Linux TPM returned an incomplete response.");

            var responseSize = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(2, sizeof(uint)));
            if (responseSize < 10 || responseSize != bytesRead)
                throw new IOException("The Linux TPM response size is invalid.");

            response = buffer.AsSpan(0, bytesRead).ToArray();
        }

        public override void Close()
        {
            _stream?.Dispose();
            _stream = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Close();
        }

        public override bool UsesTbs() => false;
        public override bool HasRM() => _HasRM;
    }
}
