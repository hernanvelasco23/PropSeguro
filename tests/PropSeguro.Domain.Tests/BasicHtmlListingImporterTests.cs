using System.Net;
using PropSeguro.Infrastructure.Listings;

namespace PropSeguro.Domain.Tests;

public sealed class BasicHtmlListingImporterTests
{
    [Fact]
    public async Task ImportAsync_when_site_returns_403_recovers_basic_data_from_url()
    {
        var httpClient = new HttpClient(new StubHandler(HttpStatusCode.Forbidden, string.Empty));
        var importer = new BasicHtmlListingImporter(httpClient);

        var result = await importer.ImportAsync("https://www.zonaprop.com.ar/propiedades/clasificado/veclapin-av-coronel-diaz-2400-venta-departamento-de-dos-56971825.html?n_src=Listado");

        Assert.Contains("Coronel Diaz 2400", result.Address);
        Assert.Contains("Venta Departamento De Dos", result.Title);
        Assert.Equal(2, result.Rooms);
        Assert.True(result.ConfidenceScore > 0);
        Assert.Contains(result.Warnings, warning => warning.Contains("403", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            });
        }
    }
}
