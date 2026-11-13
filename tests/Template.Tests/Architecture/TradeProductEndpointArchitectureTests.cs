using System;
using System.Collections.Generic;
using System.Linq;
using Faranou.Modules.Trade.Application.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Server.Api.Trade;
using Template.Tests.Integration;
using Xunit;

namespace Template.Tests.Architecture;

public sealed class TradeProductEndpointArchitectureTests : IClassFixture<FaranouApplicationFactory>
{
    private readonly FaranouApplicationFactory factory;

    public TradeProductEndpointArchitectureTests(FaranouApplicationFactory factory) => this.factory = factory;

    private RouteEndpoint[] Endpoints(string prefix) => factory.Services.GetRequiredService<EndpointDataSource>()
        .Endpoints.OfType<RouteEndpoint>()
        .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
        .ToArray();

    [Fact]
    public void ExactlyTheFourProductRoutesExistEachWithTradeMetadataAndNoDeleteRoute()
    {
        var expected = new Dictionary<string, (string Permission, TradeAccessIntent Intent, bool PeriodIndependent)>
        {
            ["GET /api/v1/trade/products"] = ("trade.form.product.view", TradeAccessIntent.Read, false),
            ["GET /api/v1/trade/products/{id:guid}"] = ("trade.form.product.view", TradeAccessIntent.Read, false),
            ["POST /api/v1/trade/products"] = ("trade.form.product.create", TradeAccessIntent.Write, true),
            ["PUT /api/v1/trade/products/{id:guid}"] = ("trade.form.product.update", TradeAccessIntent.Write, true),
        };

        var actual = Endpoints("/api/v1/trade/products").ToDictionary(
            endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText!.TrimEnd('/')}",
            endpoint => endpoint);

        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, endpoint) in actual)
        {
            var operation = Assert.Single(endpoint.Metadata.GetOrderedMetadata<TradeOperationMetadata>());
            Assert.Equal(expected[key].Permission, operation.PermissionCode);
            Assert.Equal(expected[key].Intent, operation.Intent);
            Assert.Equal(expected[key].PeriodIndependent, operation.PeriodIndependent);
            Assert.NotNull(endpoint.Metadata.GetMetadata<TradeGateMarker>());
            if (operation.PeriodIndependent)
            {
                Assert.Equal(["trade.form.product.view"], operation.AdditionalReadPermissions);
            }
        }
    }

    [Fact]
    public void UnitsReferenceRouteIsGetReadWithProductViewPermissionAndTradeMetadata()
    {
        var endpoint = Assert.Single(Endpoints("/api/v1/trade/reference/units"));

        Assert.Equal(["GET"], endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.ToArray());
        var operation = Assert.Single(endpoint.Metadata.GetOrderedMetadata<TradeOperationMetadata>());
        Assert.Equal("trade.form.product.view", operation.PermissionCode);
        Assert.Equal(TradeAccessIntent.Read, operation.Intent);
        Assert.False(operation.PeriodIndependent);
        Assert.NotNull(endpoint.Metadata.GetMetadata<TradeGateMarker>());
    }

    [Fact]
    public void ProductApplicationLayerHasNoEntityFrameworkReference()
    {
        var references = typeof(Faranou.Modules.Trade.Application.Products.ProductService).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }
}
