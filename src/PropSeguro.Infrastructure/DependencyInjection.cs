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
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString) ||
            connectionString.Contains("__SET_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' must be configured with a real value.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(30);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            }));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());
        services.Configure<PdfOcrOptions>(options =>
        {
            options.PdfToPpmPath = configuration["PdfOcr:PdfToPpmPath"];
            options.TessDataPath = configuration["PdfOcr:TessDataPath"];
            options.Language = configuration["PdfOcr:Language"] ?? options.Language;

            if (int.TryParse(configuration["PdfOcr:Dpi"], out var dpi))
            {
                options.Dpi = dpi;
            }

            if (int.TryParse(configuration["PdfOcr:MaxPages"], out var maxPages))
            {
                options.MaxPages = maxPages;
            }
        });
        services.AddScoped<IPdfTextExtractor, PdfPigTextExtractor>();
        services.AddScoped<IPdfOcrTextExtractor, PdfOcrTextExtractor>();
        services.AddScoped<IListingImporter>(_ => new BasicHtmlListingImporter(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        }));

        return services;
    }
}
