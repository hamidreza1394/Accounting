using Infrastructure.Provisioning;
using Xunit;

namespace Template.Tests;

public sealed class CustomerProvisioningParentClaimResolverTests
{
    [Theory]
    [InlineData("a0b1c2d3-e4f5-4678-9012-abcdefabcdef")]
    [InlineData("A0B1C2D3-E4F5-4678-9012-ABCDEFABCDEF")]
    public void TryParseRequestId_AcceptsCanonicalGuidDCaseInsensitively(string value)
    {
        Assert.True(CustomerProvisioningParentClaimResolver.TryParseRequestId(value, out var requestId));
        Assert.Equal(Guid.Parse("a0b1c2d3-e4f5-4678-9012-abcdefabcdef"), requestId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("parent-001")]
    [InlineData("{a0b1c2d3-e4f5-4678-9012-abcdefabcdef}")]
    [InlineData("a0b1c2de4f546789012abcdefabcdef")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void TryParseRequestId_RejectsNonCanonicalOrEmptyReferences(string? value)
    {
        Assert.False(CustomerProvisioningParentClaimResolver.TryParseRequestId(value, out var requestId));
        Assert.Equal(Guid.Empty, requestId);
    }
}
