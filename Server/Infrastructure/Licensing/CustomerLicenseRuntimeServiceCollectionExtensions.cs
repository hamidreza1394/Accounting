using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Licensing;

/// <summary>
/// DI registration for the shared customer runtime license evaluator.
/// Keeps Program.cs free of detailed registration noise while ensuring the
/// revocation-aware path is the only exposed runtime evaluation entry point.
/// </summary>
public static class CustomerLicenseRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddCustomerLicenseRuntimeEvaluator(this IServiceCollection services)
    {
        // Shared runtime entry point: always re-validates the stored envelope through the
        // revocation-aware ICustomerLicenseEnvelopeValidator. No path may trust the DB row alone.
        services.AddScoped<ICustomerLicenseRuntimeEvaluator, CustomerLicenseRuntimeEvaluator>();
        return services;
    }
}
