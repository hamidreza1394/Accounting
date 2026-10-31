using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Server.Api.Trade;
using Template.Tests.Integration;
using Xunit;

namespace Template.Tests.Architecture;

/// <summary>
/// Guards that no Trade route can exist without the fail-closed access gate, and that no
/// permissive entitlement ships in production assemblies.
/// </summary>
public sealed class TradeEndpointGateTests : IClassFixture<FaranouApplicationFactory>
{
    private readonly FaranouApplicationFactory factory;

    public TradeEndpointGateTests(FaranouApplicationFactory factory) => this.factory = factory;

    private RouteEndpoint[] AllRouteEndpoints() =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();

    private RouteEndpoint[] TradeEndpoints() => AllRouteEndpoints()
        .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1/trade", StringComparison.OrdinalIgnoreCase) == true)
        .ToArray();

    private static string[] Verbs(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.ToArray() ?? [];

    [Fact]
    public void TradeRouteSetIsNotEmpty() => Assert.NotEmpty(TradeEndpoints());

    [Fact]
    public void EveryTradeEndpointHasTheGateMarkerAndExactlyOneAccessDeclaration()
    {
        foreach (var endpoint in TradeEndpoints())
        {
            var name = endpoint.RoutePattern.RawText;
            Assert.True(endpoint.Metadata.GetMetadata<TradeGateMarker>() is not null, $"{name} lacks the gate marker.");

            var operations = endpoint.Metadata.GetOrderedMetadata<TradeOperationMetadata>().Count;
            var capabilities = endpoint.Metadata.GetOrderedMetadata<TradeCapabilitiesEndpointMetadata>().Count;
            Assert.True(operations + capabilities == 1, $"{name} must declare exactly one of operation/capabilities metadata.");
        }
    }

    [Fact]
    public void CapabilitiesMarkerIsOnlyOnGetCapabilities()
    {
        foreach (var endpoint in AllRouteEndpoints()
                     .Where(endpoint => endpoint.Metadata.GetMetadata<TradeCapabilitiesEndpointMetadata>() is not null))
        {
            Assert.Equal("/api/v1/trade/capabilities", endpoint.RoutePattern.RawText);
            Assert.Equal(["GET"], Verbs(endpoint));
        }

        Assert.Contains(TradeEndpoints(), endpoint => endpoint.RoutePattern.RawText == "/api/v1/trade/capabilities");
    }

    [Fact]
    public void ReadVerbsDeclareReadIntentAndMutatingVerbsDeclareWriteIntent()
    {
        foreach (var endpoint in TradeEndpoints())
        {
            var operation = endpoint.Metadata.GetMetadata<TradeOperationMetadata>();
            if (operation is null)
            {
                continue;
            }

            var verbs = Verbs(endpoint);
            Assert.NotEmpty(verbs);
            foreach (var verb in verbs)
            {
                var expected = verb is "GET" or "HEAD" ? TradeAccessIntent.Read : TradeAccessIntent.Write;
                Assert.True(expected == operation.Intent, $"{verb} {endpoint.RoutePattern.RawText} declares {operation.Intent}.");
            }

            Assert.True(TradePermissionCodes.IsAccessIntentCompatible(operation.PermissionCode, operation.Intent));
        }
    }

    [Fact]
    public void NoTradeEndpointIsAnonymous()
    {
        foreach (var endpoint in TradeEndpoints())
        {
            Assert.Null(endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>());
            Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>());
        }
    }

    [Fact]
    public void NoTradeRouteExistsOutsideTheGatedGroup()
    {
        var outside = AllRouteEndpoints()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains("trade", StringComparison.OrdinalIgnoreCase) == true)
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1/trade", StringComparison.OrdinalIgnoreCase) != true)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.Empty(outside);
    }

    [Fact]
    public void RequireTradePermissionRejectsIncompatibleIntentAtStartup()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        var endpoint = app.MapGet("/x", () => "x");

        Assert.Throws<InvalidOperationException>(() =>
            endpoint.RequireTradePermission(TradePermissionCodes.FormView("a"), TradeAccessIntent.Write));
        Assert.Throws<InvalidOperationException>(() =>
            endpoint.RequireTradePermission("trade.form.a.unknown", TradeAccessIntent.Read));
    }

    [Fact]
    public void ExactlyOneEntitlementImplementationShipsAndItDeniesAll()
    {
        Type[] assemblyAnchors =
        [
            typeof(Program),
            typeof(ITradeModuleEntitlement),
            typeof(Faranou.Modules.Trade.Infrastructure.Persistence.TradeDbContext),
        ];

        var implementations = assemblyAnchors
            .Select(type => type.Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ITradeModuleEntitlement).IsAssignableFrom(type))
            .ToArray();

        var only = Assert.Single(implementations);
        Assert.Equal(typeof(Infrastructure.Trade.DenyAllTradeModuleEntitlement), only);
    }

    [Fact]
    public void EveryEndpointCarryingTradeAccessMetadataAlsoCarriesTheGateMarker()
    {
        foreach (var endpoint in AllRouteEndpoints().Where(endpoint =>
                     endpoint.Metadata.GetMetadata<TradeOperationMetadata>() is not null ||
                     endpoint.Metadata.GetMetadata<TradeCapabilitiesEndpointMetadata>() is not null))
        {
            Assert.True(
                endpoint.Metadata.GetMetadata<TradeGateMarker>() is not null,
                $"{endpoint.RoutePattern.RawText} declares Trade access metadata outside the gated Trade group.");
        }
    }

    [Fact]
    public void OnlyTheGateAndTheAdapterDependOnTheTradeAuthorizationPort()
    {
        // ITradeAuthorization alone is not entitlement-aware; endpoints and use cases must go
        // through TradeAccessGate.
        var allowed = new[] { typeof(TradeAccessGate), typeof(Infrastructure.Trade.TradeAuthorizationAdapter) };
        var assemblies = new[]
        {
            typeof(Infrastructure.Trade.TradeAuthorizationAdapter).Assembly,
            typeof(TradeAccessGate).Assembly,
        };

        var offenders = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => !allowed.Contains(type) && !type.IsInterface)
            .Where(type => type.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType == typeof(ITradeAuthorization)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(offenders);
    }
}
