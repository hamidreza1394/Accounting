using Dtat.Security;
using Xunit;

namespace Template.Tests;

public sealed class BaselineTests
{
    [Fact]
    public void LegacySha256Helper_ReturnsExpectedBase64Digest()
    {
        var digest = Hashing.GetSha256("abc");

        Assert.Equal("ungWv48Bz+pBQUDeXa4iI7ADYaOWF3qctBD/YfIAFa0=", digest);
    }
}
