using Infrastructure.Provisioning;
using Xunit;

namespace Template.Tests;

public sealed class CustomerProvisioningEnrollmentTokenTests
{
    [Fact]
    public void Generate_ReturnsCanonicalHighEntropyBase64UrlToken()
    {
        var token = CustomerProvisioningEnrollmentToken.Generate();

        Assert.Equal(CustomerProvisioningEnrollmentToken.EncodedTokenLength, token.Length);
        Assert.All(token, character =>
            Assert.True(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
        Assert.Equal(token, CustomerProvisioningEnrollmentToken.Generate());
    }

    [Fact]
    public void ComputeHash_StoresDigestNotRawToken_AndVerifiesIt()
    {
        var token = CustomerProvisioningEnrollmentToken.Generate();
        var hash = CustomerProvisioningEnrollmentToken.ComputeHash(token);

        Assert.Equal(CustomerProvisioningEnrollmentToken.HashHexLength, hash.Length);
        Assert.DoesNotContain(token, hash, StringComparison.Ordinal);
        Assert.True(CustomerProvisioningEnrollmentToken.Verify(token, hash));
        Assert.False(CustomerProvisioningEnrollmentToken.Verify(
            CustomerProvisioningEnrollmentToken.Generate(), hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHijk")]
    public void Verify_RejectsMalformedToken(string? token)
    {
        Assert.False(CustomerProvisioningEnrollmentToken.Verify(token, new string('A', 64)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-hex")]
    [InlineData("0123456789")]
    public void Verify_RejectsMalformedStoredDigest(string? storedHash)
    {
        Assert.False(CustomerProvisioningEnrollmentToken.Verify(
            CustomerProvisioningEnrollmentToken.Generate(), storedHash));
    }

    [Fact]
    public void Verify_RejectsNonCanonicalBase64UrlEncoding()
    {
        var token = CustomerProvisioningEnrollmentToken.Generate();
        var nonCanonical = token[..^1] + 'B';

        Assert.False(CustomerProvisioningEnrollmentToken.Verify(
            nonCanonical, CustomerProvisioningEnrollmentToken.ComputeHash(token)));
    }

    [Fact]
    public void ComputeHash_RejectsMalformedToken()
    {
        Assert.Throws<ArgumentException>(() =>
            CustomerProvisioningEnrollmentToken.ComputeHash("not-a-valid-token"));
    }
}
