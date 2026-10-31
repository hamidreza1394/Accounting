using System;
using Infrastructure.Licensing;
using Microsoft.Extensions.Options;
using Xunit;

namespace Template.Tests.Licensing;

public sealed class Tpm2NvExtendRollbackAnchorTests
{
    [Fact]
    public void Constructor_RejectsMissingNvIndexConfiguration()
    {
        var options = Options.Create(new TpmNvExtendOptions
        {
            NvIndexOffset = 0,
            NvIndexAuthValueBase64 = Convert.ToBase64String([1, 2, 3])
        });

        Assert.Throws<InvalidOperationException>(() => new Tpm2NvExtendRollbackAnchor(options));
    }

    [Fact]
    public void Constructor_RejectsMissingAuthorization()
    {
        var options = Options.Create(new TpmNvExtendOptions
        {
            NvIndexOffset = 0x1234,
            NvIndexAuthValueBase64 = null
        });

        Assert.Throws<InvalidOperationException>(() => new Tpm2NvExtendRollbackAnchor(options));
    }

    [Fact]
    public void Constructor_RejectsInvalidAuthorizationEncoding()
    {
        var options = Options.Create(new TpmNvExtendOptions
        {
            NvIndexOffset = 0x1234,
            NvIndexAuthValueBase64 = "not-base64"
        });

        Assert.Throws<InvalidOperationException>(() => new Tpm2NvExtendRollbackAnchor(options));
    }

    [Fact]
    public void Constructor_RejectsAuthorizationLongerThanSha256Digest()
    {
        var options = Options.Create(new TpmNvExtendOptions
        {
            NvIndexOffset = 0x1234,
            NvIndexAuthValueBase64 = Convert.ToBase64String(new byte[33])
        });

        Assert.Throws<InvalidOperationException>(() => new Tpm2NvExtendRollbackAnchor(options));
    }

    [Fact]
    public void Constructor_RejectsNonLocalLinuxDevicePath()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var options = Options.Create(new TpmNvExtendOptions
        {
            NvIndexOffset = 0x1234,
            NvIndexAuthValueBase64 = Convert.ToBase64String([1, 2, 3]),
            LinuxDevicePath = "tcp://127.0.0.1:2321"
        });

        Assert.Throws<InvalidOperationException>(() => new Tpm2NvExtendRollbackAnchor(options));
    }
}
