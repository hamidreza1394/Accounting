using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Faranou.Modules.Trade.Application.Authorization;
using Faranou.Modules.Trade.Application.Parties;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Server.Api.Trade;
using Template.Tests.Integration;
using Xunit;

namespace Template.Tests.Architecture;

public sealed class TradePartyEndpointArchitectureTests : IClassFixture<FaranouApplicationFactory>
{
    private readonly FaranouApplicationFactory factory;

    public TradePartyEndpointArchitectureTests(FaranouApplicationFactory factory) => this.factory = factory;

    private RouteEndpoint[] PartyEndpoints() => factory.Services.GetRequiredService<EndpointDataSource>()
        .Endpoints.OfType<RouteEndpoint>()
        .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1/trade/parties", StringComparison.OrdinalIgnoreCase) == true)
        .ToArray();

    [Fact]
    public void ExactlyTheFourPartyRoutesExistEachWithTradeMetadata()
    {
        var expected = new Dictionary<string, (string Permission, TradeAccessIntent Intent)>
        {
            ["GET /api/v1/trade/parties"] = ("trade.form.party.view", TradeAccessIntent.Read),
            ["GET /api/v1/trade/parties/{id:guid}"] = ("trade.form.party.view", TradeAccessIntent.Read),
            ["POST /api/v1/trade/parties"] = ("trade.form.party.create", TradeAccessIntent.Write),
            ["PUT /api/v1/trade/parties/{id:guid}"] = ("trade.form.party.update", TradeAccessIntent.Write),
        };

        var actual = PartyEndpoints().ToDictionary(
            endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText!.TrimEnd('/')}",
            endpoint => endpoint);

        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, endpoint) in actual)
        {
            var operation = Assert.Single(endpoint.Metadata.GetOrderedMetadata<TradeOperationMetadata>());
            Assert.Equal(expected[key].Permission, operation.PermissionCode);
            Assert.Equal(expected[key].Intent, operation.Intent);
            Assert.NotNull(endpoint.Metadata.GetMetadata<TradeGateMarker>());
        }
    }

    [Fact]
    public void CountriesReferenceRouteIsGetReadWithViewPermissionAndTradeMetadata()
    {
        var endpoint = Assert.Single(
            factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>(),
            item => item.RoutePattern.RawText == "/api/v1/trade/reference/countries");

        Assert.Equal(["GET"], endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.ToArray());
        var operation = Assert.Single(endpoint.Metadata.GetOrderedMetadata<TradeOperationMetadata>());
        Assert.Equal("trade.form.party.view", operation.PermissionCode);
        Assert.Equal(TradeAccessIntent.Read, operation.Intent);
        Assert.NotNull(endpoint.Metadata.GetMetadata<TradeGateMarker>());
    }

    [Fact]
    public void OnlyPartyWritesArePeriodIndependentAndTheyAlsoRequireView()
    {
        var all = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<TradeOperationMetadata>() is not null)
            .ToArray();

        var periodIndependent = all
            .Where(endpoint => endpoint.Metadata.GetMetadata<TradeOperationMetadata>()!.PeriodIndependent)
            .Select(endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText!.TrimEnd('/')}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["POST /api/v1/trade/parties", "PUT /api/v1/trade/parties/{id:guid}"], periodIndependent);
        foreach (var endpoint in all.Where(endpoint => endpoint.Metadata.GetMetadata<TradeOperationMetadata>()!.PeriodIndependent))
        {
            var operation = endpoint.Metadata.GetMetadata<TradeOperationMetadata>()!;
            Assert.Equal(TradeAccessIntent.Write, operation.Intent);
            Assert.Equal(["trade.form.party.view"], operation.AdditionalReadPermissions);
        }
    }

    [Fact]
    public void RequireTradePermissionRejectsInvalidPeriodIndependentOrAdditionalPermissions()
    {
        var app = WebApplication.CreateBuilder().Build();
        var read = app.MapGet("/r", () => "x");
        var write = app.MapPost("/w", () => "x");

        Assert.Throws<InvalidOperationException>(() =>
            read.RequireTradePermission(TradePermissionCodes.FormView("a"), TradeAccessIntent.Read, periodIndependent: true));
        Assert.Throws<InvalidOperationException>(() =>
            write.RequireTradePermission(TradePermissionCodes.FormUpdate("a"), TradeAccessIntent.Write, alsoRequireRead: [TradePermissionCodes.FormUpdate("a")]));
    }

    [Fact]
    public void OnlyTradeAccessGateCallsTheAuthorizationPortWhichIsTheOnlyWayToRequestPeriodIndependence()
    {
        // IL scan: every call of ITradeAuthorization.HasPermissionAsync in the host and Trade assemblies must come
        // from TradeAccessGate (the only place that may pass periodIndependent: true).
        var target = typeof(ITradeAuthorization).GetMethod(nameof(ITradeAuthorization.HasPermissionAsync))!;
        var assemblies = new[]
        {
            typeof(Program).Assembly,
            typeof(TradeAccessGate).Assembly,
            typeof(Faranou.Modules.Trade.Infrastructure.Persistence.TradeDbContext).Assembly,
        }.Distinct();

        var callers = new List<string>();
        foreach (var type in assemblies.SelectMany(assembly => assembly.GetTypes()))
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                     BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
            {
                var il = method.GetMethodBody()?.GetILAsByteArray();
                if (il is null)
                {
                    continue;
                }

                for (var i = 0; i + 4 < il.Length; i++)
                {
                    if (il[i] is not (0x28 or 0x6F))
                    {
                        continue;
                    }

                    try
                    {
                        if (method.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1)) == target)
                        {
                            callers.Add((type.DeclaringType ?? type).FullName!);
                            break;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Not a method token; keep scanning.
                    }
                }
            }
        }

        // Positive control: the scan really sees the gate's call (its async state machine).
        Assert.Contains(typeof(TradeAccessGate).FullName, callers);
        Assert.All(callers, caller => Assert.Equal(typeof(TradeAccessGate).FullName, caller));
    }

    [Fact]
    public void TradeApplicationHasNoEntityFrameworkReference()
    {
        var references = typeof(PartyService).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal));
    }
}
