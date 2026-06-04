using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropSeguro.Application.Common;
using PropSeguro.Infrastructure.Documents;
using PropSeguro.Infrastructure.Listings;
using PropSeguro.Infrastructure.Persistence;

namespace PropSeguro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IPdfTextExtractor, PdfPigTextExtractor>();
        services.AddScoped<IListingImporter>(_ => new BasicHtmlListingImporter(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        }));

        return services;
    }
}
