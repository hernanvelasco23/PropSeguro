using Microsoft.Extensions.DependencyInjection;
using PropSeguro.Application.Common;
using PropSeguro.Application.PropertyAnalyses.Reports;

namespace PropSeguro.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddScoped<IExpenseIntelligenceAnalyzer, DeterministicExpenseIntelligenceAnalyzer>();
        services.AddScoped<IDocumentIntelligenceAnalyzer, DeterministicDocumentIntelligenceAnalyzer>();
        services.AddScoped<IPropertyReportBuilder, DeterministicPropertyReportBuilder>();
        services.AddScoped<IPropertyReportPdfGenerator, PropertyReportPdfGenerator>();

        return services;
    }
}
