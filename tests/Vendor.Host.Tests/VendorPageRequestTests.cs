using Faranou.Vendor.Host.Authorization;
using Xunit;

namespace Faranou.Vendor.Host.Tests;

public sealed class VendorPageRequestTests
{
    [Fact]
    public void Normalize_UsesDefaultsWhenValuesAreMissing()
    {
        var result = VendorPageRequest.Normalize(null, null);

        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(0, result.Skip);
    }

    [Theory]
    [InlineData(0, 0, 1, 25)]
    [InlineData(-10, -2, 1, 25)]
    [InlineData(3, 10, 3, 10)]
    [InlineData(2, 1000, 2, 100)]
    public void Normalize_ClampsOutOfRangeValues(int? page, int? pageSize, int expectedPage, int expectedSize)
    {
        var result = VendorPageRequest.Normalize(page, pageSize);

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedSize, result.PageSize);
    }

    [Fact]
    public void Normalize_CapsLargePageToRepresentableOffset()
    {
        var result = VendorPageRequest.Normalize(int.MaxValue, 100);

        Assert.Equal((int)((long)int.MaxValue / 100) + 1, result.Page);
        Assert.Equal((int)(((long)int.MaxValue / 100) * 100), result.Skip);
        Assert.True(result.Skip >= 0);
    }

    [Fact]
    public void PageResponse_ReportsNavigationMetadata()
    {
        var result = new VendorPageResponse<string>(["a", "b"], 2, 2, 5);

        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void EmptyPageResponse_HasZeroTotalPages()
    {
        var result = new VendorPageResponse<string>(Array.Empty<string>(), 1, 25, 0);

        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }
}
