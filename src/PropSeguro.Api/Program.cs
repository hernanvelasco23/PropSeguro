using MediatR;
using System.Text.Json.Serialization;
using PropSeguro.Application;
using PropSeguro.Application.Common;
using PropSeguro.Application.PropertyAnalyses.AnalyzePropertyDocument;
using PropSeguro.Application.PropertyAnalyses.AddPropertyDocument;
using PropSeguro.Application.PropertyAnalyses.CreatePropertyAnalysis;
using PropSeguro.Application.PropertyAnalyses.DeletePropertyAnalysis;
using PropSeguro.Application.PropertyAnalyses.GetPropertyAnalysis;
using PropSeguro.Application.PropertyAnalyses.SavePropertyDocumentText;
using PropSeguro.Application.PropertyAnalyses.ListPropertyAnalyses;
using PropSeguro.Application.PropertyAnalyses.UpdatePropertyAnalysis;
using PropSeguro.Application.PropertyAnalyses.UploadPropertyCoverImage;
using PropSeguro.Domain.PropertyAnalyses;
using PropSeguro.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("DevelopmentFrontend");
}

app.UseHttpsRedirection();

var propertyAnalyses = app.MapGroup("/api/property-analyses")
    .WithTags("Property Analyses");

app.MapPost("/api/listing-imports", async (
    ListingImportRequest request,
    IListingImporter listingImporter,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await listingImporter.ImportAsync(request.ListingUrl, cancellationToken);
        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new
        {
            error = exception.Message
        });
    }
})
.WithTags("Listing Imports")
.WithName("ImportListing");

propertyAnalyses.MapPost("/", async (
    CreatePropertyAnalysisRequest request,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await sender.Send(new CreatePropertyAnalysisCommand(
            request.Title,
            request.ListingUrl,
            request.Address,
            request.Neighborhood,
            request.PriceUsd,
            request.MonthlyExpenses,
            request.TotalSquareMeters,
            request.CoveredSquareMeters,
            request.Rooms,
            request.Bedrooms,
            request.Bathrooms,
            request.BuildingAgeYears,
            request.Floor,
            request.HasBalcony,
            request.HasAmenities,
            request.IsCreditEligible,
            request.PropertyCondition,
            request.Notes), cancellationToken);

        return Results.Created($"/api/property-analyses/{result.Id}", result);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new
        {
            error = exception.Message
        });
    }
})
.WithName("CreatePropertyAnalysis");

propertyAnalyses.MapGet("", async (
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new ListPropertyAnalysesQuery(), cancellationToken);
    return Results.Ok(result);
})
.WithName("ListPropertyAnalyses");

propertyAnalyses.MapGet("/{id:guid}", async (
    Guid id,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetPropertyAnalysisQuery(id), cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
})
.WithName("GetPropertyAnalysis");

propertyAnalyses.MapPut("/{id:guid}", async (
    Guid id,
    UpdatePropertyAnalysisRequest request,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await sender.Send(new UpdatePropertyAnalysisCommand(
            id,
            request.Title,
            request.ListingUrl,
            request.Address,
            request.Neighborhood,
            request.PriceUsd,
            request.MonthlyExpenses,
            request.TotalSquareMeters,
            request.CoveredSquareMeters,
            request.Rooms,
            request.Bedrooms,
            request.Bathrooms,
            request.BuildingAgeYears,
            request.Floor,
            request.HasBalcony,
            request.HasAmenities,
            request.IsCreditEligible,
            request.PropertyCondition,
            request.Notes), cancellationToken);

        return result is null ? Results.NotFound() : Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new
        {
            error = exception.Message
        });
    }
})
.WithName("UpdatePropertyAnalysis");

propertyAnalyses.MapDelete("/{id:guid}", async (
    Guid id,
    ISender sender,
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    var deleted = await sender.Send(new DeletePropertyAnalysisCommand(id), cancellationToken);

    if (!deleted)
    {
        return Results.NotFound();
    }

    var analysisDirectory = Path.Combine(
        Program.GetStorageRoot(environment.ContentRootPath),
        "storage",
        "property-analyses",
        id.ToString("D"));

    try
    {
        if (Directory.Exists(analysisDirectory))
        {
            Directory.Delete(analysisDirectory, recursive: true);
        }
    }
    catch
    {
        // File cleanup is best-effort; the database delete is the source of truth.
    }

    return Results.NoContent();
})
.WithName("DeletePropertyAnalysis");

propertyAnalyses.MapPost("/{id:guid}/cover-image", async (
    Guid id,
    HttpRequest request,
    ISender sender,
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new
        {
            error = "The request must be multipart/form-data."
        });
    }

    var existingAnalysis = await sender.Send(new GetPropertyAnalysisQuery(id), cancellationToken);

    if (existingAnalysis is null)
    {
        return Results.NotFound();
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file");

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new
        {
            error = "An image file is required."
        });
    }

    if (file.Length > Program.MaxCoverImageSizeBytes)
    {
        return Results.BadRequest(new
        {
            error = "The cover image must be 5 MB or smaller."
        });
    }

    if (!Program.IsAllowedCoverImageContentType(file.ContentType))
    {
        return Results.BadRequest(new
        {
            error = "Only JPEG, PNG or WEBP images are allowed."
        });
    }

    var extension = Program.GetCoverImageExtension(file.ContentType, file.FileName);
    var safeFileName = $"cover-{Guid.NewGuid():N}{extension}";
    var storagePath = string.Join(
        '/',
        "storage",
        "property-analyses",
        id.ToString("D"),
        "cover",
        safeFileName);
    var fullPath = Path.Combine(Program.GetStorageRoot(environment.ContentRootPath), storagePath.Replace('/', Path.DirectorySeparatorChar));

    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

    await using (var stream = File.Create(fullPath))
    {
        await file.CopyToAsync(stream, cancellationToken);
    }

    try
    {
        var result = await sender.Send(new UploadPropertyCoverImageCommand(
            id,
            Path.GetFileName(file.FileName),
            file.ContentType,
            file.Length,
            storagePath), cancellationToken);

        if (result is null)
        {
            File.Delete(fullPath);
            return Results.NotFound();
        }

        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        File.Delete(fullPath);
        return Results.BadRequest(new
        {
            error = exception.Message
        });
    }
    catch
    {
        File.Delete(fullPath);
        throw;
    }
})
.WithName("UploadPropertyCoverImage");

propertyAnalyses.MapGet("/{id:guid}/cover-image", async Task<IResult> (
    Guid id,
    ISender sender,
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    var analysis = await sender.Send(new GetPropertyAnalysisQuery(id), cancellationToken);

    if (analysis?.CoverImageStoragePath is null || analysis.CoverImageContentType is null)
    {
        return Results.NotFound();
    }

    var fullPath = Path.Combine(
        Program.GetStorageRoot(environment.ContentRootPath),
        analysis.CoverImageStoragePath.Replace('/', Path.DirectorySeparatorChar));

    if (!File.Exists(fullPath))
    {
        return Results.NotFound();
    }

    var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
    return Results.File(bytes, analysis.CoverImageContentType, analysis.CoverImageFileName);
})
.WithName("GetPropertyCoverImage");

propertyAnalyses.MapPost("/{id:guid}/documents", async (
    Guid id,
    HttpRequest request,
    ISender sender,
    IWebHostEnvironment environment,
    IPdfTextExtractor pdfTextExtractor,
    IPdfOcrTextExtractor pdfOcrTextExtractor,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new
        {
            error = "The request must be multipart/form-data."
        });
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var documentTypeValue = form["documentType"].ToString();
    var file = form.Files.GetFile("file");

    if (!Enum.TryParse<DocumentType>(documentTypeValue, ignoreCase: true, out var documentType))
    {
        return Results.BadRequest(new
        {
            error = "A valid documentType is required."
        });
    }

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new
        {
            error = "A PDF file is required."
        });
    }

    var documentId = Guid.NewGuid();
    var storagePath = string.Join(
        '/',
        "storage",
        "property-analyses",
        id.ToString("D"),
        "documents",
        documentId.ToString("D"),
        "original.pdf");
    var fullPath = Path.Combine(Program.GetStorageRoot(environment.ContentRootPath), storagePath.Replace('/', Path.DirectorySeparatorChar));

    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

    await using (var stream = File.Create(fullPath))
    {
        await file.CopyToAsync(stream, cancellationToken);
    }

    string? extractedText = null;
    string? extractionError = null;
    TextExtractionQuality? textExtractionQuality = null;
    string? extractionSummary = null;

    if (string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            extractedText = await pdfTextExtractor.ExtractTextAsync(fullPath, cancellationToken);
            textExtractionQuality = DocumentTextQualityEvaluator.Evaluate(documentType, extractedText);

            if (DocumentTextQualityEvaluator.ShouldAttemptOcr(documentType, extractedText))
            {
                var ocrResult = await pdfOcrTextExtractor.ExtractTextAsync(fullPath, documentType, cancellationToken);

                if (!string.IsNullOrWhiteSpace(ocrResult.Text))
                {
                    extractedText = ocrResult.Text;
                    textExtractionQuality = ocrResult.Quality;
                }

                extractionSummary = ocrResult.Warning;
            }
        }
        catch
        {
            extractionError = "No se pudo extraer texto del PDF. La carga se guardo correctamente; podes pegar texto manualmente para analizarlo.";
        }
    }

    try
    {
        var result = await sender.Send(new AddPropertyDocumentCommand(
            id,
            documentId,
            documentType,
            Path.GetFileName(file.FileName),
            file.ContentType,
            file.Length,
            storagePath,
            extractedText,
            extractionError,
            textExtractionQuality,
            extractionSummary), cancellationToken);

        if (result is null)
        {
            File.Delete(fullPath);
            return Results.NotFound();
        }

        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        File.Delete(fullPath);
        return Results.BadRequest(new
        {
            error = exception.Message
        });
    }
    catch
    {
        File.Delete(fullPath);
        throw;
    }
})
.WithName("AddPropertyDocument");

propertyAnalyses.MapPost("/{analysisId:guid}/documents/{documentId:guid}/analyze", async (
    Guid analysisId,
    Guid documentId,
    AnalyzePropertyDocumentRequest request,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new AnalyzePropertyDocumentCommand(
        analysisId,
        documentId,
        request.ExtractedText), cancellationToken);

    return result is null ? Results.NotFound() : Results.Ok(result);
})
.WithName("AnalyzePropertyDocument");

propertyAnalyses.MapPut("/{analysisId:guid}/documents/{documentId:guid}/text", async (
    Guid analysisId,
    Guid documentId,
    SavePropertyDocumentTextRequest request,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new SavePropertyDocumentTextCommand(
        analysisId,
        documentId,
        request.EditedText), cancellationToken);

    return result is null ? Results.NotFound() : Results.Ok(result);
})
.WithName("SavePropertyDocumentText");

app.Run();

public sealed record CreatePropertyAnalysisRequest(
    string Title,
    string? ListingUrl,
    string Address,
    string Neighborhood,
    decimal? PriceUsd,
    decimal? MonthlyExpenses,
    decimal? TotalSquareMeters,
    decimal? CoveredSquareMeters,
    int? Rooms,
    int? Bedrooms,
    int? Bathrooms,
    int? BuildingAgeYears,
    int? Floor,
    bool? HasBalcony,
    bool? HasAmenities,
    bool? IsCreditEligible,
    PropertyCondition? PropertyCondition,
    string? Notes);

public sealed record UpdatePropertyAnalysisRequest(
    string Title,
    string? ListingUrl,
    string Address,
    string Neighborhood,
    decimal? PriceUsd,
    decimal? MonthlyExpenses,
    decimal? TotalSquareMeters,
    decimal? CoveredSquareMeters,
    int? Rooms,
    int? Bedrooms,
    int? Bathrooms,
    int? BuildingAgeYears,
    int? Floor,
    bool? HasBalcony,
    bool? HasAmenities,
    bool? IsCreditEligible,
    PropertyCondition? PropertyCondition,
    string? Notes);

public sealed record ListingImportRequest(string ListingUrl);

public sealed record AnalyzePropertyDocumentRequest(string? ExtractedText);

public sealed record SavePropertyDocumentTextRequest(string? EditedText);

public partial class Program
{
    public const long MaxCoverImageSizeBytes = 5 * 1024 * 1024;

    internal static string GetStorageRoot(string contentRootPath)
    {
        var directory = new DirectoryInfo(contentRootPath);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PropSeguro.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return contentRootPath;
    }

    internal static bool IsAllowedCoverImageContentType(string? contentType)
    {
        return string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "image/webp", StringComparison.OrdinalIgnoreCase);
    }

    internal static string GetCoverImageExtension(string contentType, string? fileName)
    {
        var extension = Path.GetExtension(fileName);

        if (!string.IsNullOrWhiteSpace(extension))
        {
            extension = extension.ToLowerInvariant();

            if (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) && extension is ".jpg" or ".jpeg")
            {
                return extension;
            }

            if (contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) && extension == ".png")
            {
                return extension;
            }

            if (contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase) && extension == ".webp")
            {
                return extension;
            }
        }

        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".img"
        };
    }
}
