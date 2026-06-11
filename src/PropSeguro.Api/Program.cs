using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using PropSeguro.Api.Auth;
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
using PropSeguro.Application.PropertyAnalyses.Reports;
using PropSeguro.Application.PropertyAnalyses.UpdatePropertyAnalysis;
using PropSeguro.Application.PropertyAnalyses.UploadPropertyCoverImage;
using PropSeguro.Domain.BetaAccess;
using PropSeguro.Domain.PropertyAnalyses;
using PropSeguro.Domain.Users;
using PropSeguro.Infrastructure;
using PropSeguro.Infrastructure.Persistence;

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
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(nameof(UserRole.Admin)));
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
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    await Program.SeedDevelopmentAdminAsync(app.Services, app.Configuration);
}

var propertyAnalyses = app.MapGroup("/api/property-analyses")
    .WithTags("Property Analyses")
    .RequireAuthorization();

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    ApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    CancellationToken cancellationToken) =>
{
    if (!new EmailAddressAttribute().IsValid(request.Email))
    {
        return Results.Unauthorized();
    }

    var email = AppUser.NormalizeEmail(request.Email);
    var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);

    if (user is null || user.Status == UserStatus.Disabled || !passwordHasher.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    if (user.Status == UserStatus.Invited)
    {
        user.Enable();
    }

    user.MarkLogin();
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new LoginResponse(
        jwtTokenService.CreateToken(user),
        UserResponse.FromUser(user)));
})
.AllowAnonymous()
.WithTags("Auth")
.WithName("Login");

app.MapPost("/api/beta-access-requests", async (
    CreateBetaAccessRequest request,
    ApplicationDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    if (!new EmailAddressAttribute().IsValid(request.Email))
    {
        return Results.BadRequest(new { error = "Email invalido." });
    }

    try
    {
        var betaRequest = new BetaAccessRequest(
            request.FullName,
            request.Email,
            request.Phone,
            request.IntendedUse,
            request.Message);

        dbContext.BetaAccessRequests.Add(betaRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/beta-access-requests/{betaRequest.Id}", new
        {
            betaRequest.Id,
            Status = betaRequest.Status.ToString()
        });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
})
.AllowAnonymous()
.WithTags("Beta Access")
.WithName("CreateBetaAccessRequest");

var admin = app.MapGroup("/api/admin")
    .RequireAuthorization("AdminOnly")
    .WithTags("Admin");

admin.MapGet("/beta-access-requests", async (ApplicationDbContext dbContext, CancellationToken cancellationToken) =>
{
    var requests = await dbContext.BetaAccessRequests
        .AsNoTracking()
        .OrderByDescending(item => item.CreatedAtUtc)
        .Select(item => BetaAccessRequestResponse.FromRequest(item))
        .ToListAsync(cancellationToken);

    return Results.Ok(requests);
})
.WithName("ListBetaAccessRequests");

admin.MapPost("/beta-access-requests/{id:guid}/approve", async (
    Guid id,
    ApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    CancellationToken cancellationToken) =>
{
    var betaRequest = await dbContext.BetaAccessRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    if (betaRequest is null)
    {
        return Results.NotFound();
    }

    betaRequest.Approve();
    var email = AppUser.NormalizeEmail(betaRequest.Email);
    var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
    string? temporaryPassword = null;

    if (user is null)
    {
        temporaryPassword = Program.CreateTemporaryPassword();
        user = new AppUser(email, betaRequest.FullName, passwordHasher.Hash(temporaryPassword), UserRole.User, UserStatus.Invited);
        dbContext.Users.Add(user);
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new ApproveBetaAccessResponse(
        BetaAccessRequestResponse.FromRequest(betaRequest),
        UserResponse.FromUser(user),
        temporaryPassword));
})
.WithName("ApproveBetaAccessRequest");

admin.MapPost("/beta-access-requests/{id:guid}/reject", async (
    Guid id,
    ApplicationDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var betaRequest = await dbContext.BetaAccessRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    if (betaRequest is null)
    {
        return Results.NotFound();
    }

    betaRequest.Reject();
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(BetaAccessRequestResponse.FromRequest(betaRequest));
})
.WithName("RejectBetaAccessRequest");

admin.MapGet("/users", async (ApplicationDbContext dbContext, CancellationToken cancellationToken) =>
{
    var users = await dbContext.Users
        .AsNoTracking()
        .OrderBy(item => item.Email)
        .Select(item => UserResponse.FromUser(item))
        .ToListAsync(cancellationToken);

    return Results.Ok(users);
})
.WithName("ListUsers");

admin.MapPost("/users", async (
    CreateUserRequest request,
    ApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    CancellationToken cancellationToken) =>
{
    if (!new EmailAddressAttribute().IsValid(request.Email))
    {
        return Results.BadRequest(new { error = "Email invalido." });
    }

    var email = AppUser.NormalizeEmail(request.Email);
    var duplicate = await dbContext.Users.AnyAsync(item => item.Email == email && item.Status != UserStatus.Disabled, cancellationToken);

    if (duplicate)
    {
        return Results.Conflict(new { error = "Ya existe un usuario activo con ese email." });
    }

    var user = new AppUser(
        email,
        request.FullName,
        passwordHasher.Hash(request.Password),
        request.Role,
        UserStatus.Active);

    dbContext.Users.Add(user);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/admin/users/{user.Id}", UserResponse.FromUser(user));
})
.WithName("CreateUser");

admin.MapPatch("/users/{id:guid}/disable", async (
    Guid id,
    ApplicationDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    if (user is null)
    {
        return Results.NotFound();
    }

    user.Disable();
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(UserResponse.FromUser(user));
})
.WithName("DisableUser");

admin.MapPatch("/users/{id:guid}/enable", async (
    Guid id,
    ApplicationDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    if (user is null)
    {
        return Results.NotFound();
    }

    user.Enable();
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(UserResponse.FromUser(user));
})
.WithName("EnableUser");

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
            request.ApartmentUnit,
            request.UnitIdentifier,
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

propertyAnalyses.MapGet("/{id:guid}/report", async (
    Guid id,
    IPropertyReportBuilder reportBuilder,
    CancellationToken cancellationToken) =>
{
    var result = await reportBuilder.BuildAsync(id, cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
})
.WithName("GetPropertyAnalysisReport");

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
            request.ApartmentUnit,
            request.UnitIdentifier,
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
.AllowAnonymous()
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
    string? ApartmentUnit,
    string? UnitIdentifier,
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
    string? ApartmentUnit,
    string? UnitIdentifier,
    bool? HasBalcony,
    bool? HasAmenities,
    bool? IsCreditEligible,
    PropertyCondition? PropertyCondition,
    string? Notes);

public sealed record ListingImportRequest(string ListingUrl);

public sealed record AnalyzePropertyDocumentRequest(string? ExtractedText);

public sealed record SavePropertyDocumentTextRequest(string? EditedText);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string Token, UserResponse User);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc)
{
    public static UserResponse FromUser(AppUser user)
    {
        return new UserResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.Role.ToString(),
            user.Status.ToString(),
            user.CreatedAtUtc,
            user.LastLoginAtUtc);
    }
}

public sealed record CreateBetaAccessRequest(
    string FullName,
    string Email,
    string? Phone,
    string? IntendedUse,
    string? Message);

public sealed record BetaAccessRequestResponse(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string? IntendedUse,
    string? Message,
    DateTime CreatedAtUtc,
    string Status)
{
    public static BetaAccessRequestResponse FromRequest(BetaAccessRequest request)
    {
        return new BetaAccessRequestResponse(
            request.Id,
            request.FullName,
            request.Email,
            request.Phone,
            request.IntendedUse,
            request.Message,
            request.CreatedAtUtc,
            request.Status.ToString());
    }
}

public sealed record ApproveBetaAccessResponse(
    BetaAccessRequestResponse Request,
    UserResponse User,
    string? TemporaryPassword);

public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    UserRole Role);

public partial class Program
{
    public const long MaxCoverImageSizeBytes = 5 * 1024 * 1024;

    internal static async Task SeedDevelopmentAdminAsync(IServiceProvider services, IConfiguration configuration)
    {
        var seedEmail = configuration["SeedAdmin:Email"];
        var seedFullName = configuration["SeedAdmin:FullName"];
        var seedPassword = configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(seedEmail) ||
            string.IsNullOrWhiteSpace(seedFullName) ||
            string.IsNullOrWhiteSpace(seedPassword))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await dbContext.Users.AnyAsync())
        {
            return;
        }

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var adminUser = new AppUser(
            seedEmail,
            seedFullName,
            passwordHasher.Hash(seedPassword),
            UserRole.Admin,
            UserStatus.Active);

        dbContext.Users.Add(adminUser);
        await dbContext.SaveChangesAsync();
    }

    internal static string CreateTemporaryPassword()
    {
        return $"PS-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}";
    }

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
