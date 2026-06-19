using Microsoft.AspNetCore.Authentication.JwtBearer;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PropSeguro.Application.Common;
using PropSeguro.Application.PropertyAnalyses;
using PropSeguro.Application.PropertyAnalyses.AddPropertyDocument;
using PropSeguro.Application.PropertyAnalyses.GetPropertyAnalysis;
using PropSeguro.Application.PropertyAnalyses.UploadPropertyCoverImage;
using PropSeguro.Api.Auth;
using PropSeguro.Api.Storage;
using PropSeguro.Domain.Users;
using PropSeguro.Infrastructure.Persistence;

namespace PropSeguro.Domain.Tests;

public sealed class PropertyAnalysisEndpointTests
{
    [Fact]
    public async Task GetPropertyAnalysis_without_token_returns_unauthorized()
    {
        await using var factory = CreateFactory(new NotFoundSender());
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/property-analyses/{Guid.NewGuid()}");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_cover_image_accepts_valid_image()
    {
        var analysisId = Guid.NewGuid();
        var sender = new CoverImageSender(analysisId);
        await using var factory = CreateFactory(sender);

        using var client = factory.CreateClient();
        Authorize(factory, client);
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

        DeleteUploadedFile(sender.LastUploadCommand.StoragePath);
    }

    [Fact]
    public async Task Upload_cover_image_rejects_invalid_content_type()
    {
        var analysisId = Guid.NewGuid();
        await using var factory = CreateFactory(new CoverImageSender(analysisId));

        using var client = factory.CreateClient();
        Authorize(factory, client);
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
        await using var factory = CreateFactory(new CoverImageSender(analysisId));

        using var client = factory.CreateClient();
        Authorize(factory, client);
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
        await using var factory = CreateFactory(new NotFoundSender());

        using var client = factory.CreateClient();
        Authorize(factory, client);
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

    private static void Authorize(WebApplicationFactory<Program> factory, HttpClient client)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokenService.CreateToken(new AppUser("test@propsegura.local", "Test User", "hash", UserRole.Admin, UserStatus.Active));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task Upload_document_rejects_invalid_content_type()
    {
        var analysisId = Guid.NewGuid();
        await using var factory = CreateFactory(new DocumentUploadSender(analysisId));
        using var client = factory.CreateClient();
        Authorize(factory, client);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("ReservationAgreement"), "documentType");
        var document = new ByteArrayContent([1, 2, 3]);
        document.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(document, "file", "reserva.txt");

        var response = await client.PostAsync($"/api/property-analyses/{analysisId}/documents", content);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_document_rejects_oversized_pdf()
    {
        var analysisId = Guid.NewGuid();
        await using var factory = CreateFactory(new DocumentUploadSender(analysisId), maxPdfSizeBytes: 3);
        using var client = factory.CreateClient();
        Authorize(factory, client);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("ReservationAgreement"), "documentType");
        var document = new ByteArrayContent([1, 2, 3, 4]);
        document.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(document, "file", "reserva.pdf");

        var response = await client.PostAsync($"/api/property-analyses/{analysisId}/documents", content);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_document_neutralizes_path_traversal_file_name()
    {
        var analysisId = Guid.NewGuid();
        var sender = new DocumentUploadSender(analysisId);
        await using var factory = CreateFactory(sender);
        using var client = factory.CreateClient();
        Authorize(factory, client);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("ReservationAgreement"), "documentType");
        var document = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]);
        document.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(document, "file", "..\\..\\evil.pdf");

        var response = await client.PostAsync($"/api/property-analyses/{analysisId}/documents", content);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(sender.LastAddCommand);
        Assert.Equal("evil.pdf", sender.LastAddCommand.FileName);
        Assert.DoesNotContain("..", sender.LastAddCommand.StoragePath);
        Assert.Contains($"/{analysisId:D}/documents/", sender.LastAddCommand.StoragePath);

        DeleteUploadedFile(sender.LastAddCommand.StoragePath);
    }

    private static WebApplicationFactory<Program> CreateFactory(ISender sender, long? maxPdfSizeBytes = null, long? maxImageSizeBytes = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Jwt:Issuer", "PropSegura.Tests");
                builder.UseSetting("Jwt:Audience", "PropSegura.Tests");
                builder.UseSetting("Jwt:SigningKey", "propsegura-tests-signing-key-with-more-than-32-bytes");
                builder.UseSetting("Email:Provider", "Null");
                builder.UseSetting("Storage:Provider", "Local");
                builder.UseSetting("Storage:RootPath", "storage");

                if (maxPdfSizeBytes.HasValue)
                {
                    builder.UseSetting("Storage:MaxPdfSizeBytes", maxPdfSizeBytes.Value.ToString());
                }

                if (maxImageSizeBytes.HasValue)
                {
                    builder.UseSetting("Storage:MaxImageSizeBytes", maxImageSizeBytes.Value.ToString());
                }

                builder.ConfigureServices(services =>
                {
                    ConfigureInMemoryDatabase(services);
                    services.AddSingleton(sender);
                });
            });
    }

    private static void ConfigureInMemoryDatabase(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<IApplicationDbContext>();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("PropertyEndpointTests-" + Guid.NewGuid()));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "propsegura-test-keys")));
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events = new JwtBearerEvents();
        });
    }

    private static void DeleteUploadedFile(string storagePath)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PropSeguro.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            return;
        }

        var fullPath = Path.Combine(directory.FullName, storagePath.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
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

        public static PropertyAnalysisDto CreateDto(
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
                null,
                null,
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
                82000,
                2.5m,
                "Rango estimado.",
                new NegotiationAnalysisDto(80000, 2000, 2100, "Fair", 76000, 84000, 76000, 78000, 82000, 2.5m, "Rango estimado."),
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
                null,
                DateTime.UtcNow,
                DateTime.UtcNow);
        }
    }

    private sealed class DocumentUploadSender(Guid analysisId) : ISender
    {
        public AddPropertyDocumentCommand? LastAddCommand { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetPropertyAnalysisQuery getQuery && getQuery.Id == analysisId)
            {
                return Task.FromResult((TResponse)(object)CoverImageSender.CreateDto(analysisId));
            }

            if (request is AddPropertyDocumentCommand addCommand && addCommand.PropertyAnalysisId == analysisId)
            {
                LastAddCommand = addCommand;
                return Task.FromResult((TResponse)(object)CoverImageSender.CreateDto(analysisId));
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
    }
}
