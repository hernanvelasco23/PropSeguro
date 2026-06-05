using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PropSeguro.Application.PropertyAnalyses;
using PropSeguro.Application.PropertyAnalyses.GetPropertyAnalysis;
using PropSeguro.Application.PropertyAnalyses.UploadPropertyCoverImage;

namespace PropSeguro.Domain.Tests;

public sealed class PropertyAnalysisEndpointTests
{
    [Fact]
    public async Task GetPropertyAnalysis_with_unknown_id_returns_not_found()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ISender, NotFoundSender>();
                });
            });

        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/property-analyses/{Guid.NewGuid()}");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_cover_image_accepts_valid_image()
    {
        var analysisId = Guid.NewGuid();
        var sender = new CoverImageSender(analysisId);
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ISender>(sender);
                });
            });

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var image = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(image, "file", "departamento.png");

        var response = await client.PostAsync($"/api/property-analyses/{analysisId}/cover-image", content);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(sender.LastUploadCommand);
        Assert.Equal(analysisId, sender.LastUploadCommand.PropertyAnalysisId);
        Assert.Equal("departamento.png", sender.LastUploadCommand.FileName);
        Assert.Equal("image/png", sender.LastUploadCommand.ContentType);
        Assert.EndsWith(".png", sender.LastUploadCommand.StoragePath);
    }

    [Fact]
    public async Task Upload_cover_image_rejects_invalid_content_type()
    {
        var analysisId = Guid.NewGuid();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ISender>(new CoverImageSender(analysisId));
                });
            });

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var image = new ByteArrayContent([1, 2, 3]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(image, "file", "not-image.txt");

        var response = await client.PostAsync($"/api/property-analyses/{analysisId}/cover-image", content);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_cover_image_rejects_files_larger_than_5_mb()
    {
        var analysisId = Guid.NewGuid();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ISender>(new CoverImageSender(analysisId));
                });
            });

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var image = new ByteArrayContent(new byte[Program.MaxCoverImageSizeBytes + 1]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(image, "file", "large.jpg");

        var response = await client.PostAsync($"/api/property-analyses/{analysisId}/cover-image", content);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_cover_image_with_unknown_property_returns_not_found()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ISender, NotFoundSender>();
                });
            });

        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var image = new ByteArrayContent([0xFF, 0xD8, 0xFF]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(image, "file", "departamento.jpg");

        var response = await client.PostAsync($"/api/property-analyses/{Guid.NewGuid()}/cover-image", content);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class NotFoundSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(default(TResponse)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<object?>(null);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return EmptyAsync<TResponse>();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            return EmptyAsync<object?>();
        }

        private static async IAsyncEnumerable<T> EmptyAsync<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class CoverImageSender(Guid analysisId) : ISender
    {
        public UploadPropertyCoverImageCommand? LastUploadCommand { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetPropertyAnalysisQuery getQuery && getQuery.Id == analysisId)
            {
                return Task.FromResult((TResponse)(object)CreateDto(analysisId));
            }

            if (request is UploadPropertyCoverImageCommand uploadCommand && uploadCommand.PropertyAnalysisId == analysisId)
            {
                LastUploadCommand = uploadCommand;
                return Task.FromResult((TResponse)(object)CreateDto(
                    analysisId,
                    uploadCommand.FileName,
                    uploadCommand.ContentType,
                    uploadCommand.SizeBytes,
                    uploadCommand.StoragePath));
            }

            return Task.FromResult(default(TResponse)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<object?>(null);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return EmptyAsync<TResponse>();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            return EmptyAsync<object?>();
        }

        private static async IAsyncEnumerable<T> EmptyAsync<T>()
        {
            await Task.CompletedTask;
            yield break;
        }

        private static PropertyAnalysisDto CreateDto(
            Guid id,
            string? coverFileName = null,
            string? coverContentType = null,
            long? coverSizeBytes = null,
            string? coverStoragePath = null)
        {
            return new PropertyAnalysisDto(
                id,
                "Departamento",
                null,
                "Av. Santa Fe 1234",
                "Palermo",
                80000,
                100000,
                40,
                36,
                2,
                1,
                1,
                10,
                3,
                true,
                true,
                true,
                "Good",
                null,
                80,
                60,
                "Low",
                "Investment",
                2000,
                "En linea con el benchmark",
                "Fair",
                76000,
                84000,
                76000,
                78000,
                2.5m,
                "Rango estimado.",
                new NegotiationAnalysisDto(80000, 2000, 2100, "Fair", 76000, 84000, 76000, 78000, 2.5m, "Rango estimado."),
                coverFileName,
                coverContentType,
                coverSizeBytes,
                coverStoragePath,
                coverStoragePath is null ? null : DateTime.UtcNow,
                coverStoragePath is null ? null : $"/api/property-analyses/{id:D}/cover-image",
                "Completed",
                [],
                [],
                [],
                [],
                DateTime.UtcNow,
                DateTime.UtcNow);
        }
    }
}
