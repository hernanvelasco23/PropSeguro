using Microsoft.Extensions.DependencyInjection;
using PropSeguro.Application.Common;

namespace PropSeguro.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddScoped<IDocumentIntelligenceAnalyzer, DeterministicDocumentIntelligenceAnalyzer>();

        return services;
    }
}
