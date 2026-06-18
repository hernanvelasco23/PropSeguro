using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using PropSeguro.Api.Auth;
using PropSeguro.Api.Email;
using PropSeguro.Api.Storage;
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
builder.Services.AddSwaggerGen(options =>
{
    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Ingresá el JWT con el formato: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    options.AddSecurityDefinition("Bearer", bearerScheme);
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var configuredAllowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray() ?? [];

var effectiveAllowedOrigins = configuredAllowedOrigins.Length == 0 && builder.Environment.IsDevelopment()
    ? ["http://localhost:5173"]
    : configuredAllowedOrigins;

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        if (effectiveAllowedOrigins.Length > 0)
        {
            policy.WithOrigins(effectiveAllowedOrigins);
        }
        else
        {
            policy.SetIsOriginAllowed(_ => false);
        }

        policy
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Demasiados intentos. Probá nuevamente más tarde."
        }, cancellationToken);
    };

    options.AddPolicy("Login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            Program.BuildRateLimitPartitionKey(context, "login"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("BetaAccessRequest", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            Program.BuildRateLimitPartitionKey(context, "beta-access-request"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
Program.ValidateJwtOptions(jwtOptions, builder.Environment);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IBetaInvitationEmailService, BetaInvitationEmailService>();
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

var storageOptions = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
if (Program.IsLocalStorageProvider(storageOptions.Provider))
{
    builder.Services.AddSingleton<IPropertyFileStorage, LocalPropertyFileStorage>();
    builder.Services.AddSingleton<IFileStorageService>(provider => provider.GetRequiredService<IPropertyFileStorage>());
}
else if (Program.IsAzureBlobStorageProvider(storageOptions.Provider))
{
    Program.ValidateAzureBlobStorageOptions(storageOptions);
    builder.Services.AddSingleton<IPropertyFileStorage, AzureBlobPropertyFileStorage>();
    builder.Services.AddSingleton<IFileStorageService>(provider => provider.GetRequiredService<IPropertyFileStorage>());
}
else
{
    throw new InvalidOperationException("Storage:Provider must be either Local or AzureBlob.");
}

var emailOptions = builder.Configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
if (Program.IsNullEmailProvider(emailOptions.Provider))
{
    builder.Services.AddSingleton<IEmailSender, NullEmailSender>();
}
else if (Program.IsSmtpEmailProvider(emailOptions.Provider))
{
    Program.ValidateSmtpEmailOptions(emailOptions);
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    throw new InvalidOperationException("Email:Provider must be either Null or Smtp.");
}

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.EffectiveSigningKey)),
            ValidateLifetime = true,
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(
                    context.Exception,
                    "JWT authentication failed. TraceId={TraceId} Method={Method} Path={Path} ExceptionMessage={ExceptionMessage}",
                    context.HttpContext.TraceIdentifier,
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path,
                    context.Exception.Message);

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();

                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "No autorizado.",
                    message = "La sesion no es valida o expiro. Volve a iniciar sesion.",
                    traceId = context.HttpContext.TraceIdentifier
                });
            },
            OnForbidden = async context =>
            {
                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Acceso denegado.",
                    message = "Tu usuario no tiene permisos para realizar esta accion.",
                    traceId = context.HttpContext.TraceIdentifier
                });
            },
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!Guid.TryParse(userIdValue, out var userId))
                {
                    context.Fail("Invalid user token.");
                    return;
                }

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var userStatus = await dbContext.Users
                    .AsNoTracking()
                    .Where(user => user.Id == userId)
                    .Select(user => (UserStatus?)user.Status)
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                if (userStatus is null || userStatus == UserStatus.Disabled)
                {
                    context.Fail("User is not active.");
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(nameof(UserRole.Admin)));
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var minimalPipeline = builder.Configuration.GetValue<bool>("Diagnostics:MinimalPipeline");
var enableSwagger = !builder.Environment.IsProduction() ||
    builder.Configuration.GetValue<bool>("Diagnostics:EnableSwagger");
var enableRateLimiter = !builder.Environment.IsProduction() ||
    builder.Configuration.GetValue<bool>("Diagnostics:EnableRateLimiter");
var enableAuth = !builder.Environment.IsProduction() ||
    builder.Configuration.GetValue("Diagnostics:EnableAuth", true);

var app = builder.Build();
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

app.MapGet("/ping", () => Results.Text("pong")).AllowAnonymous();

if (minimalPipeline)
{
    app.Run();
    return;
}

startupLogger.LogInformation(
    "CORS DefaultCorsPolicy allowed origins: {AllowedOrigins}",
    effectiveAllowedOrigins.Length > 0 ? string.Join(", ", effectiveAllowedOrigins) : "(none)");

if (!enableAuth)
{
    startupLogger.LogWarning(
        "Authentication and authorization middleware are disabled. Authenticated endpoints may fail unless Diagnostics:EnableAuth is true.");
}

app.UseRouting();
app.UseCors("DefaultCorsPolicy");

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception exception)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = context.User.FindFirstValue(ClaimTypes.Email);
        var userRole = context.User.FindFirstValue(ClaimTypes.Role);

        logger.LogError(
            exception,
            "Unhandled backend exception. TraceId={TraceId} Method={Method} Path={Path} Origin={Origin} UserId={UserId} UserEmail={UserEmail} UserRole={UserRole} ExceptionMessage={ExceptionMessage}",
            context.TraceIdentifier,
            context.Request.Method,
            context.Request.Path,
            context.Request.Headers.Origin.ToString(),
            userId,
            userEmail,
            userRole,
            exception.Message);

        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Error interno del backend.",
            message = "La operacion fallo en el servidor. Revisa los logs de App Service con el traceId para ver la causa exacta.",
            traceId = context.TraceIdentifier
        });
    }
});

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

if (enableRateLimiter)
{
    app.UseRateLimiter();
}

if (enableAuth)
{
    app.UseAuthentication();
    app.Use(async (context, next) =>
    {
        var endpoint = context.GetEndpoint();
        var requiresAuthorization = endpoint?.Metadata.GetMetadata<IAuthorizeData>() is not null;

        if (requiresAuthorization)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = context.User.FindFirstValue(ClaimTypes.Email);
            var roles = string.Join(",", context.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value));

            logger.LogInformation(
                "Authorized endpoint request. TraceId={TraceId} Method={Method} Path={Path} IsAuthenticated={IsAuthenticated} UserId={UserId} UserEmail={UserEmail} Roles={Roles}",
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path,
                context.User.Identity?.IsAuthenticated == true,
                userId,
                userEmail,
                string.IsNullOrWhiteSpace(roles) ? "(none)" : roles);
        }

        await next();
    });
    app.UseAuthorization();
}

var seedAdminEnabled = app.Configuration.GetValue<bool>("SeedAdmin:Enabled");
var shouldRunSeedAdmin = app.Environment.IsDevelopment() || seedAdminEnabled;

if (shouldRunSeedAdmin)
{
    try
    {
        await Program.SeedDevelopmentAdminAsync(app.Services, app.Configuration, app.Environment);
    }
    catch (Exception exception)
    {
        startupLogger.LogError(
            exception,
            "SeedAdmin startup task failed. Environment={Environment} SeedAdminEnabled={SeedAdminEnabled} ExceptionMessage={ExceptionMessage}",
            app.Environment.EnvironmentName,
            seedAdminEnabled,
            exception.Message);
    }
}

app.MapGet("/health", () => Results.Text("OK"))
    .AllowAnonymous()
    .WithTags("Diagnostics")
    .WithName("Health");

app.MapGet("/debug/config", (
    IConfiguration configuration,
    IWebHostEnvironment environment) =>
{
    return Results.Ok(Program.BuildDebugConfiguration(configuration, environment));
})
.AllowAnonymous()
.WithTags("Diagnostics")
.WithName("GetDebugConfiguration");

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
.RequireRateLimiting("Login")
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
.RequireRateLimiting("BetaAccessRequest")
.WithTags("Beta Access")
.WithName("CreateBetaAccessRequest");

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/dev/email-test", async (
        SendTestEmailRequest request,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        if (!new EmailAddressAttribute().IsValid(request.ToEmail))
        {
            return Results.BadRequest(new { error = "Email inválido." });
        }

        var emailOptions = configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();

        if (!Program.IsSmtpEmailProvider(emailOptions.Provider))
        {
            return Results.BadRequest(new
            {
                emailSent = false,
                message = "El proveedor SMTP no está habilitado. Configurá Email:Provider=Smtp para probar el envío real."
            });
        }

        try
        {
            await emailSender.SendAsync(new EmailMessage(
                request.ToEmail,
                request.ToName,
                "Prueba SMTP PropSegura",
                """
                <p>Este es un correo de prueba de PropSegura.</p>
                <p>Si recibiste este mensaje, la conexión SMTP está funcionando.</p>
                """,
                "Este es un correo de prueba de PropSegura. Si recibiste este mensaje, la conexión SMTP está funcionando."),
                cancellationToken);

            return Results.Ok(new
            {
                emailSent = true,
                message = $"Correo de prueba enviado a {request.ToEmail}."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Development SMTP test failed for provider {Provider}, host {Host}, port {Port}, to {ToEmail}.",
                emailOptions.Provider,
                emailOptions.Smtp.Host,
                emailOptions.Smtp.Port,
                request.ToEmail);

            return Results.BadRequest(new
            {
                emailSent = false,
                message = "No se pudo enviar el correo de prueba. Revisá host, puerto, SSL, usuario y contraseña SMTP.",
                diagnostics = Program.BuildSmtpDiagnostics(emailOptions, exception)
            });
        }
    })
    .RequireAuthorization("AdminOnly")
    .WithTags("Development")
    .WithName("SendDevelopmentTestEmail");

    app.MapGet("/api/dev/config/email", (
        IConfiguration configuration) =>
    {
        var emailOptions = configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();

        return Results.Ok(new
        {
            provider = emailOptions.Provider,
            fromEmail = emailOptions.FromEmail,
            smtp = new
            {
                host = emailOptions.Smtp.Host,
                port = emailOptions.Smtp.Port,
                enableSsl = emailOptions.Smtp.EnableSsl,
                username = emailOptions.Smtp.Username,
                passwordConfigured = !Program.IsPlaceholderSecret(emailOptions.Smtp.Password),
                passwordLooksPlaceholder = Program.IsPlaceholderSecret(emailOptions.Smtp.Password),
                passwordSource = Program.FindConfigurationValueSource(configuration, "Email:Smtp:Password")
            },
            configurationProviders = Program.GetConfigurationProviderNames(configuration)
        });
    })
    .RequireAuthorization("AdminOnly")
    .WithTags("Development")
    .WithName("GetDevelopmentEmailConfiguration");
}

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
    IBetaInvitationEmailService betaInvitationEmailService,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var betaRequest = await dbContext.BetaAccessRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    if (betaRequest is null)
    {
        return Results.NotFound();
    }

    var email = AppUser.NormalizeEmail(betaRequest.Email);
    var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);

    if (betaRequest.Status == BetaAccessRequestStatus.Approved)
    {
        return Results.Ok(new ApproveBetaAccessResponse(
            betaRequest.Id,
            betaRequest.Status.ToString(),
            user?.Id ?? Guid.Empty,
            email,
            false,
            null,
            "La solicitud ya estaba aprobada.",
            null));
    }

    string? temporaryPassword = null;
    var shouldSendInvitation = false;

    if (user is null)
    {
        temporaryPassword = Program.CreateTemporaryPassword();
        user = new AppUser(email, betaRequest.FullName, passwordHasher.Hash(temporaryPassword), UserRole.User, UserStatus.Invited);
        dbContext.Users.Add(user);
        shouldSendInvitation = true;
    }
    else if (user.Status != UserStatus.Active)
    {
        temporaryPassword = Program.CreateTemporaryPassword();
        user.UpdatePasswordHash(passwordHasher.Hash(temporaryPassword));
        user.Enable();
        shouldSendInvitation = true;
    }

    betaRequest.Approve();
    await dbContext.SaveChangesAsync(cancellationToken);

    var emailOptions = configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
    var emailSent = false;
    string? emailError = null;
    string? message = null;

    if (shouldSendInvitation && temporaryPassword is not null && Program.IsNullEmailProvider(emailOptions.Provider))
    {
        message = "Email deshabilitado en este entorno. Copiá la contraseña temporal manualmente.";
    }
    else if (shouldSendInvitation && temporaryPassword is not null)
    {
        try
        {
            var loginUrl = Program.BuildLoginUrl(configuration);
            await betaInvitationEmailService.SendInvitationAsync(user, temporaryPassword, loginUrl, cancellationToken);
            emailSent = true;
            message = $"Invitación enviada a {user.Email}.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not send beta invitation email to {Email}.", user.Email);
            emailError = "No se pudo enviar el email de invitación.";
            message = "Usuario aprobado, pero no se pudo enviar el email.";
        }
    }
    else
    {
        message = "Solicitud aprobada. El usuario ya existía, por eso no se generó una nueva contraseña temporal.";
    }

    var shouldReturnTemporaryPassword = Program.ShouldReturnTemporaryPassword(
        environment,
        emailOptions,
        emailSent,
        temporaryPassword);

    return Results.Ok(new ApproveBetaAccessResponse(
        betaRequest.Id,
        betaRequest.Status.ToString(),
        user.Id,
        user.Email,
        emailSent,
        emailError,
        message,
        shouldReturnTemporaryPassword ? temporaryPassword : null));
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

    if (!Program.IsValidPassword(request.Password))
    {
        return Results.BadRequest(new { error = "La contrasena debe tener al menos 8 caracteres, una letra y un numero." });
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
    ILogger<Program> logger,
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
    catch (Exception exception)
    {
        logger.LogError(
            exception,
            "Listing import failed for {ListingUrl}. ExceptionMessage: {ExceptionMessage}",
            request.ListingUrl,
            exception.Message);

        return Results.Json(
            new
            {
                error = "No se pudo importar la publicacion.",
                message = "El sitio puede estar bloqueando la lectura automatica o devolviendo un formato inesperado. Carga los datos manualmente y volve a intentar mas tarde."
            },
            statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithTags("Listing Imports")
.WithName("ImportListing")
.RequireAuthorization();

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

propertyAnalyses.MapGet("/{id:guid}/report/pdf", async (
    Guid id,
    IPropertyReportBuilder reportBuilder,
    IPropertyReportPdfGenerator pdfGenerator,
    IPropertyFileStorage fileStorage,
    CancellationToken cancellationToken) =>
{
    var report = await reportBuilder.BuildAsync(id, cancellationToken);

    if (report is null)
    {
        return Results.NotFound();
    }

    string? temporaryCoverPath = null;

    try
    {
        if (!string.IsNullOrWhiteSpace(report.CoverSection.CoverImageStoragePath))
        {
            var storedCover = await fileStorage.OpenReadAsync(report.CoverSection.CoverImageStoragePath, cancellationToken);

            if (storedCover is not null)
            {
                await using (storedCover.Content)
                {
                    temporaryCoverPath = Program.CreateTemporaryFilePath(Program.GetFileExtensionFromStoragePath(report.CoverSection.CoverImageStoragePath));
                    await using var temporaryCover = File.Create(temporaryCoverPath);
                    await storedCover.Content.CopyToAsync(temporaryCover, cancellationToken);
                }
            }
        }

        var reportForPdf = temporaryCoverPath is null
            ? report
            : report with
            {
                CoverSection = report.CoverSection with
                {
                    CoverImageStoragePath = temporaryCoverPath
                }
            };

        var bytes = pdfGenerator.Generate(reportForPdf);
        var fileName = Program.BuildReportFileName(report.PropertySummary.Title, report.GeneratedAtUtc);
        return Results.File(bytes, "application/pdf", fileName);
    }
    finally
    {
        Program.TryDeleteFile(temporaryCoverPath);
    }
})
.WithName("DownloadPropertyAnalysisReportPdf");

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
    IPropertyFileStorage fileStorage,
    CancellationToken cancellationToken) =>
{
    var deleted = await sender.Send(new DeletePropertyAnalysisCommand(id), cancellationToken);

    if (!deleted)
    {
        return Results.NotFound();
    }

    try
    {
        await fileStorage.DeletePrefixAsync(Program.BuildAnalysisStoragePrefix(id), cancellationToken);
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
    IPropertyFileStorage fileStorage,
    IOptions<StorageOptions> storageOptions,
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

    var maxImageSizeBytes = Program.GetMaxImageSizeBytes(storageOptions.Value);
    if (file.Length > maxImageSizeBytes)
    {
        return Results.BadRequest(new
        {
            error = $"The cover image must be {Program.FormatMegabytes(maxImageSizeBytes)} MB or smaller."
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
    var storagePath = fileStorage.CreateStoragePath(
        Program.BuildAnalysisStoragePrefix(id),
        "cover",
        safeFileName);

    await using (var stream = file.OpenReadStream())
    {
        await fileStorage.SaveAsync(storagePath, stream, file.ContentType, cancellationToken);
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
            await fileStorage.DeleteAsync(storagePath, cancellationToken);
            return Results.NotFound();
        }

        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        await fileStorage.DeleteAsync(storagePath, cancellationToken);
        return Results.BadRequest(new
        {
            error = exception.Message
        });
    }
    catch
    {
        await fileStorage.DeleteAsync(storagePath, cancellationToken);
        throw;
    }
})
.WithName("UploadPropertyCoverImage");

propertyAnalyses.MapGet("/{id:guid}/cover-image", async Task<IResult> (
    Guid id,
    ISender sender,
    IPropertyFileStorage fileStorage,
    CancellationToken cancellationToken) =>
{
    var analysis = await sender.Send(new GetPropertyAnalysisQuery(id), cancellationToken);

    if (analysis?.CoverImageStoragePath is null || analysis.CoverImageContentType is null)
    {
        return Results.NotFound();
    }

    var storedFile = await fileStorage.OpenReadAsync(analysis.CoverImageStoragePath, cancellationToken);
    if (storedFile is null)
    {
        return Results.NotFound();
    }

    return Results.File(storedFile.Content, analysis.CoverImageContentType, analysis.CoverImageFileName);
})
.WithName("GetPropertyCoverImage");

propertyAnalyses.MapGet("/{analysisId:guid}/documents/{documentId:guid}/file", async Task<IResult> (
    Guid analysisId,
    Guid documentId,
    ApplicationDbContext dbContext,
    ICurrentUserContext currentUser,
    IPropertyFileStorage fileStorage,
    CancellationToken cancellationToken) =>
{
    var analysis = await dbContext.PropertyAnalyses
        .Include(item => item.Documents)
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == analysisId, cancellationToken);

    if (analysis is null || !currentUser.CanAccess(analysis))
    {
        return Results.NotFound();
    }

    var document = analysis.Documents.FirstOrDefault(item => item.Id == documentId);
    if (document is null)
    {
        return Results.NotFound();
    }

    var storedFile = await fileStorage.OpenReadAsync(document.StoragePath, cancellationToken);
    if (storedFile is null)
    {
        return Results.NotFound();
    }

    return Results.File(storedFile.Content, document.ContentType, document.FileName);
})
.WithName("GetPropertyDocumentFile");

propertyAnalyses.MapPost("/{id:guid}/documents", async (
    Guid id,
    HttpRequest request,
    ISender sender,
    IPropertyFileStorage fileStorage,
    IOptions<StorageOptions> storageOptions,
    IPdfTextExtractor pdfTextExtractor,
    IPdfOcrTextExtractor pdfOcrTextExtractor,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    string? temporaryFilePath = null;
    string? storagePath = null;
    var fileStored = false;
    var fileName = string.Empty;
    var contentType = string.Empty;
    long fileLength = 0;

    try
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

        fileName = Path.GetFileName(file.FileName);
        contentType = file.ContentType;
        fileLength = file.Length;

        if (!Program.IsAllowedDocumentUploadContentType(file.ContentType))
        {
            return Results.BadRequest(new
            {
                error = "Only PDF, JPEG or PNG documents are allowed."
            });
        }

        var maxPdfSizeBytes = Program.GetMaxPdfSizeBytes(storageOptions.Value);
        if (file.Length > maxPdfSizeBytes)
        {
            return Results.BadRequest(new
            {
                error = $"The PDF document must be {Program.FormatMegabytes(maxPdfSizeBytes)} MB or smaller."
            });
        }

        var documentId = Guid.NewGuid();
        storagePath = fileStorage.CreateStoragePath(
            Program.BuildAnalysisStoragePrefix(id),
            "documents",
            documentId.ToString("D"),
            $"original{Program.GetDocumentUploadExtension(file.ContentType, file.FileName)}");
        temporaryFilePath = Program.CreateTemporaryFilePath(Program.GetDocumentUploadExtension(file.ContentType, file.FileName));

        await using (var stream = File.Create(temporaryFilePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        await using (var stream = File.OpenRead(temporaryFilePath))
        {
            await fileStorage.SaveAsync(storagePath, stream, file.ContentType, cancellationToken);
            fileStored = true;
        }

        string? extractedText = null;
        string? extractionError = null;
        TextExtractionQuality? textExtractionQuality = null;
        string? extractionSummary = null;

        if (string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                extractedText = await pdfTextExtractor.ExtractTextAsync(temporaryFilePath, cancellationToken);
                textExtractionQuality = DocumentTextQualityEvaluator.Evaluate(documentType, extractedText);

                if (DocumentTextQualityEvaluator.ShouldAttemptOcr(documentType, extractedText))
                {
                    var ocrResult = await pdfOcrTextExtractor.ExtractTextAsync(temporaryFilePath, documentType, cancellationToken);

                    if (!string.IsNullOrWhiteSpace(ocrResult.Text))
                    {
                        extractedText = ocrResult.Text;
                        textExtractionQuality = ocrResult.Quality;
                        extractionError = null;
                    }

                    if (string.IsNullOrWhiteSpace(ocrResult.Text) &&
                        textExtractionQuality is TextExtractionQuality.Empty or TextExtractionQuality.Poor)
                    {
                        extractionError = ocrResult.Warning;
                    }
                    else
                    {
                        extractionSummary = ocrResult.Warning;
                    }
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "PDF extraction failed during document upload. AnalysisId={AnalysisId} StoragePath={StoragePath} FileName={FileName} ContentType={ContentType} SizeBytes={SizeBytes} Exception={Exception}",
                    id,
                    storagePath,
                    fileName,
                    contentType,
                    fileLength,
                    exception.ToString());

                extractionError = "No se pudo extraer texto del PDF. La carga se guardo correctamente; podes pegar texto manualmente para analizarlo.";
            }
        }
        else if (Program.IsAllowedDocumentImageContentType(file.ContentType))
        {
            var ocrResult = await pdfOcrTextExtractor.ExtractTextAsync(temporaryFilePath, documentType, cancellationToken);

            if (!string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                extractedText = ocrResult.Text;
                textExtractionQuality = ocrResult.Quality;
                extractionSummary = ocrResult.Warning;
            }
            else
            {
                extractionError = ocrResult.Warning ?? "No se pudo extraer texto de la imagen. Revisa la configuracion OCR y pega el texto manualmente.";
                textExtractionQuality = TextExtractionQuality.Empty;
            }
        }

        var result = await sender.Send(new AddPropertyDocumentCommand(
            id,
            documentId,
            documentType,
            fileName,
            file.ContentType,
            file.Length,
            storagePath,
            extractedText,
            extractionError,
            textExtractionQuality,
            extractionSummary), cancellationToken);

        if (result is null)
        {
            if (fileStored)
            {
                await fileStorage.DeleteAsync(storagePath, cancellationToken);
            }

            return Results.NotFound();
        }

        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        if (fileStored && storagePath is not null)
        {
            await Program.TryDeleteStoredFileAsync(fileStorage, storagePath, logger, cancellationToken);
        }

        return Results.BadRequest(new
        {
            error = exception.Message
        });
    }
    catch (Exception exception)
    {
        if (fileStored && storagePath is not null)
        {
            await Program.TryDeleteStoredFileAsync(fileStorage, storagePath, logger, cancellationToken);
        }

        logger.LogError(
            exception,
            "Document upload failed. AnalysisId={AnalysisId} StorageProvider={StorageProvider} AzureBlobConnectionStringConfigured={AzureBlobConnectionStringConfigured} AzureBlobContainerName={AzureBlobContainerName} StoragePath={StoragePath} FileName={FileName} ContentType={ContentType} SizeBytes={SizeBytes} TemporaryFilePath={TemporaryFilePath} Exception={Exception}",
            id,
            storageOptions.Value.Provider,
            !string.IsNullOrWhiteSpace(storageOptions.Value.AzureBlob.ConnectionString) &&
                !Program.IsPlaceholderSecret(storageOptions.Value.AzureBlob.ConnectionString),
            storageOptions.Value.AzureBlob.ContainerName,
            storagePath,
            fileName,
            contentType,
            fileLength,
            temporaryFilePath,
            exception.ToString());

        return Results.Problem(
            title: "No se pudo cargar el documento.",
            detail: "La carga fallo en el backend. Revisa los logs de App Service con el traceId para ver la excepcion real.",
            statusCode: StatusCodes.Status500InternalServerError,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = request.HttpContext.TraceIdentifier
            });
    }
    finally
    {
        Program.TryDeleteFile(temporaryFilePath);
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

public sealed record SendTestEmailRequest(string ToEmail, string? ToName);

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
    Guid RequestId,
    string Status,
    Guid UserId,
    string Email,
    bool EmailSent,
    string? EmailError,
    string? Message,
    string? TemporaryPassword);

public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    UserRole Role);

public partial class Program
{
    public const long MaxCoverImageSizeBytes = 5 * 1024 * 1024;

    internal static async Task SeedDevelopmentAdminAsync(IServiceProvider services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var seedEmail = configuration["SeedAdmin:Email"];
        var seedFullName = configuration["SeedAdmin:FullName"];
        var seedPassword = configuration["SeedAdmin:Password"];
        var seedEnabled = configuration.GetValue<bool>("SeedAdmin:Enabled");

        if (string.IsNullOrWhiteSpace(seedEmail) ||
            string.IsNullOrWhiteSpace(seedFullName) ||
            string.IsNullOrWhiteSpace(seedPassword))
        {
            return;
        }

        if (environment.IsProduction() && seedEnabled && !IsValidPassword(seedPassword))
        {
            throw new InvalidOperationException("SeedAdmin is enabled in Production but SeedAdmin:Password is not strong enough.");
        }

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var normalizedEmail = AppUser.NormalizeEmail(seedEmail);
        var adminUser = await dbContext.Users.FirstOrDefaultAsync(user => user.Email == normalizedEmail);

        if (adminUser is null)
        {
            adminUser = new AppUser(
                seedEmail,
                seedFullName,
                passwordHasher.Hash(seedPassword),
                UserRole.Admin,
                UserStatus.Active);
            dbContext.Users.Add(adminUser);
        }
        else
        {
            adminUser.UpdateProfile(seedFullName);
            adminUser.UpdatePasswordHash(passwordHasher.Hash(seedPassword));
            adminUser.MakeAdmin();
            adminUser.Enable();
        }

        await dbContext.SaveChangesAsync();

        if (environment.IsDevelopment() && configuration.GetValue<bool>("DemoData:Enabled"))
        {
            await SeedDevelopmentDemoPropertyAsync(scope.ServiceProvider, adminUser);
        }
    }

    private static async Task SeedDevelopmentDemoPropertyAsync(IServiceProvider services, AppUser owner)
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var fileStorage = services.GetRequiredService<IPropertyFileStorage>();
        const string demoTitle = "Demo - Venta Departamento 2 Amb. en Palermo";

        var exists = await dbContext.PropertyAnalyses
            .AsNoTracking()
            .AnyAsync(analysis => analysis.UserId == owner.Id && analysis.Title == demoTitle);

        if (exists)
        {
            return;
        }

        var analysis = new PropertyAnalysis(
            demoTitle,
            "https://www.zonaprop.com.ar/propiedades/demo-palermo-propsegura.html",
            "Sánchez de Bustamante 1638",
            "Palermo",
            80000m,
            100000m,
            40m,
            36m,
            2,
            1,
            1,
            10,
            3,
            "B",
            "3B",
            true,
            true,
            true,
            PropertyCondition.Good,
            "Propiedad demo para revisar el flujo completo de oportunidad, documentación, hallazgos e informe.");

        analysis.AssignOwner(owner.Id);
        analysis.RunInitialAnalysis();

        var prefix = BuildAnalysisStoragePrefix(analysis.Id);
        var coverBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAADAAAAAwCAIAAADYYG7QAAAAvklEQVR4nO3YQQqAIBBF0dn9j9YhKPiQrbmQfSBbH0YRKwgjb2athU29ow0g/e8AkABIAECAhVOEX1Yp21Ztz9X6yIrINucl5ml5hPfdLHDg41g2rnTjYSLOa9o6OsHibSAo+hUMR6ufR6ifSTOd8HE0Kdpc5jvtouAQjGWqYIEFA8wzBL3BggHmGYLeYMGA8wxBb7BggHmGoDdYMMC8Q9AbLBhgniHoDRYMqB9JJ+Ro0dpoFMCyN1KxQhW5ZQq5JQo5JQo5JQr5AdpFKJVMf8cSAAAAAElFTkSuQmCC");
        var coverStoragePath = fileStorage.CreateStoragePath(prefix, "cover", "demo-cover.png");

        await using (var coverStream = new MemoryStream(coverBytes))
        {
            await fileStorage.SaveAsync(coverStoragePath, coverStream, "image/png", CancellationToken.None);
        }

        analysis.SetCoverImage("demo-cover.png", "image/png", coverBytes.Length, coverStoragePath);

        var expenseDocument = analysis.AddDocument(
            Guid.NewGuid(),
            DocumentType.Expenses,
            "expensas-demo.pdf",
            "application/pdf",
            512,
            fileStorage.CreateStoragePath(prefix, "documents", Guid.NewGuid().ToString("D"), "original.pdf"));
        var expenseText = """
        Liquidación de expensas unidad 3B. Expensas ordinarias $100.000.
        Se informa fondo de reserva, reparación de ascensor, impermeabilización de fachada y expensas extraordinarias por obra pendiente.
        La administración registra morosidad general del consorcio y solicita revisar actas.
        """;
        expenseDocument.StoreExtractedText(expenseText, TextExtractionQuality.Good);
        var expenseFindings = expenseDocument.CompleteAnalysis(null);

        var reservationDocument = analysis.AddDocument(
            Guid.NewGuid(),
            DocumentType.ReservationAgreement,
            "reserva-demo.pdf",
            "application/pdf",
            512,
            fileStorage.CreateStoragePath(prefix, "documents", Guid.NewGuid().ToString("D"), "original.pdf"));
        var reservationText = """
        Reserva de compra por el inmueble sito en Sánchez de Bustamante 1638, Palermo.
        La suma entregada será no reintegrable si el comprador desiste. La inmobiliaria retendrá la reserva y podrá aplicar penalidad.
        El plazo para avanzar será de 72 horas. La documentación será presentada posteriormente.
        """;
        reservationDocument.StoreExtractedText(reservationText, TextExtractionQuality.Good);
        var reservationFindings = reservationDocument.CompleteAnalysis(null);
        var findings = expenseFindings.Concat(reservationFindings).ToArray();

        analysis.RecalculateProtectionScore(findings);
        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.DocumentFindings.AddRange(findings);

        var demoPdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4\n% PropSegura demo placeholder\n%%EOF");
        foreach (var document in analysis.Documents)
        {
            await using var documentStream = new MemoryStream(demoPdfBytes);
            await fileStorage.SaveAsync(document.StoragePath, documentStream, "application/pdf", CancellationToken.None);
        }

        await dbContext.SaveChangesAsync();
    }

    internal static string CreateTemporaryPassword()
    {
        return $"PS-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}";
    }

    internal static string BuildRateLimitPartitionKey(HttpContext context, string policyName)
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrWhiteSpace(remoteIp))
        {
            remoteIp = "unknown";
        }

        return $"{policyName}:{remoteIp}";
    }

    internal static bool IsValidPassword(string? password)
    {
        return !string.IsNullOrWhiteSpace(password) &&
            password.Length >= 8 &&
            password.Any(char.IsLetter) &&
            password.Any(char.IsDigit);
    }

    internal static bool IsNullEmailProvider(string? provider)
    {
        return string.IsNullOrWhiteSpace(provider) ||
            provider.Equals("Null", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsSmtpEmailProvider(string? provider)
    {
        return provider?.Equals("Smtp", StringComparison.OrdinalIgnoreCase) == true;
    }

    internal static bool IsLocalStorageProvider(string? provider)
    {
        return string.IsNullOrWhiteSpace(provider) ||
            provider.Equals("Local", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAzureBlobStorageProvider(string? provider)
    {
        return provider?.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase) == true;
    }

    internal static void ValidateAzureBlobStorageOptions(StorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AzureBlob.ConnectionString) ||
            string.IsNullOrWhiteSpace(options.AzureBlob.ContainerName))
        {
            throw new InvalidOperationException("Storage provider AzureBlob requires Storage:AzureBlob:ConnectionString and Storage:AzureBlob:ContainerName.");
        }
    }

    internal static void ValidateSmtpEmailOptions(EmailOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.FromEmail) ||
            string.IsNullOrWhiteSpace(options.Smtp.Host) ||
            options.Smtp.Port <= 0 ||
            string.IsNullOrWhiteSpace(options.Smtp.Username) ||
            Program.IsPlaceholderSecret(options.Smtp.Password))
        {
            throw new InvalidOperationException("Email provider Smtp requires Email:FromEmail and complete Email:Smtp configuration. Set Email:Smtp:Password from user secrets in Development or environment variables in Production.");
        }
    }

    internal static object BuildSmtpDiagnostics(EmailOptions options, Exception exception)
    {
        return new
        {
            provider = options.Provider,
            fromEmail = options.FromEmail,
            host = options.Smtp.Host,
            port = options.Smtp.Port,
            enableSsl = options.Smtp.EnableSsl,
            username = options.Smtp.Username,
            passwordConfigured = !string.IsNullOrWhiteSpace(options.Smtp.Password) &&
                !Program.IsPlaceholderSecret(options.Smtp.Password),
            passwordLooksPlaceholder = Program.IsPlaceholderSecret(options.Smtp.Password),
            errorType = exception.GetType().Name,
            errorMessage = Program.BuildSafeSmtpErrorMessage(exception)
        };
    }

    internal static string? FindConfigurationValueSource(IConfiguration configuration, string key)
    {
        if (configuration is not IConfigurationRoot configurationRoot)
        {
            return null;
        }

        foreach (var provider in configurationRoot.Providers.Reverse())
        {
            if (provider.TryGet(key, out var value))
            {
                return string.IsNullOrWhiteSpace(value)
                    ? $"{provider.GetType().Name} (empty)"
                    : provider.GetType().Name;
            }
        }

        return null;
    }

    internal static IReadOnlyCollection<string> GetConfigurationProviderNames(IConfiguration configuration)
    {
        return configuration is IConfigurationRoot configurationRoot
            ? configurationRoot.Providers.Select(provider => provider.GetType().Name).ToArray()
            : [];
    }

    internal static IReadOnlyDictionary<string, object?> BuildDebugConfiguration(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        var emailOptions = configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
        var storageOptions = configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();

        return new Dictionary<string, object?>
        {
            ["Environment"] = environment.EnvironmentName,
            ["HasConnectionString"] = !string.IsNullOrWhiteSpace(connectionString) &&
                !Program.IsPlaceholderSecret(connectionString),
            ["ConnectionStringDatabaseName"] = Program.TryGetDatabaseName(connectionString),
            ["JwtIssuer"] = jwtOptions.Issuer,
            ["JwtAudience"] = jwtOptions.Audience,
            ["HasJwtSecret"] = !string.IsNullOrWhiteSpace(jwtOptions.Secret) &&
                !Program.IsPlaceholderSecret(jwtOptions.Secret),
            ["HasJwtSigningKey"] = !string.IsNullOrWhiteSpace(jwtOptions.SigningKey) &&
                !Program.IsPlaceholderSecret(jwtOptions.SigningKey),
            ["EmailProvider"] = emailOptions.Provider,
            ["EmailFromEmail"] = emailOptions.FromEmail,
            ["EmailSmtpHost"] = emailOptions.Smtp.Host,
            ["EmailSmtpPort"] = emailOptions.Smtp.Port,
            ["HasEmailSmtpUsername"] = !string.IsNullOrWhiteSpace(emailOptions.Smtp.Username),
            ["HasEmailSmtpPassword"] = !string.IsNullOrWhiteSpace(emailOptions.Smtp.Password) &&
                !Program.IsPlaceholderSecret(emailOptions.Smtp.Password),
            ["StorageProvider"] = storageOptions.Provider,
            ["HasStorageAzureBlobConnectionString"] = !string.IsNullOrWhiteSpace(storageOptions.AzureBlob.ConnectionString) &&
                !Program.IsPlaceholderSecret(storageOptions.AzureBlob.ConnectionString),
            ["StorageAzureBlobContainerName"] = storageOptions.AzureBlob.ContainerName
        };
    }

    internal static string? TryGetDatabaseName(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || Program.IsPlaceholderSecret(connectionString))
        {
            return null;
        }

        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            return Program.TryGetConnectionStringValue(builder, "Initial Catalog") ??
                Program.TryGetConnectionStringValue(builder, "Database");
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? TryGetConnectionStringValue(DbConnectionStringBuilder builder, string key)
    {
        return builder.TryGetValue(key, out var value) && value is not null
            ? value.ToString()
            : null;
    }

    internal static bool IsPlaceholderSecret(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
            value.Contains("__SET_", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("<password", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("TU_PASSWORD", StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildSafeSmtpErrorMessage(Exception exception)
    {
        var message = exception.Message;

        if (string.IsNullOrWhiteSpace(message))
        {
            return "Error SMTP sin mensaje.";
        }

        return message
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }

    internal static void ValidateJwtOptions(JwtOptions options, IWebHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.Audience) ||
            string.IsNullOrWhiteSpace(options.EffectiveSigningKey))
        {
            throw new InvalidOperationException("Jwt configuration requires Issuer, Audience and Secret or SigningKey.");
        }

        if (!environment.IsProduction())
        {
            return;
        }

        var signingKey = options.EffectiveSigningKey.Trim();
        var isPlaceholder = signingKey.Contains("__SET_", StringComparison.OrdinalIgnoreCase) ||
            signingKey.StartsWith("dev-only", StringComparison.OrdinalIgnoreCase);

        if (isPlaceholder || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("Production Jwt:SigningKey must be set from a strong secret with at least 32 bytes.");
        }
    }

    internal static string BuildLoginUrl(IConfiguration configuration)
    {
        var explicitLoginUrl = configuration["Frontend:LoginUrl"];

        if (!string.IsNullOrWhiteSpace(explicitLoginUrl))
        {
            return explicitLoginUrl.Trim();
        }

        var baseUrl = configuration["Frontend:BaseUrl"];

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return $"{baseUrl.Trim().TrimEnd('/')}/login";
        }

        return "http://localhost:5173/login";
    }

    internal static bool ShouldReturnTemporaryPassword(
        IWebHostEnvironment environment,
        EmailOptions emailOptions,
        bool emailSent,
        string? temporaryPassword)
    {
        if (string.IsNullOrWhiteSpace(temporaryPassword))
        {
            return false;
        }

        return environment.IsDevelopment() ||
            Program.IsNullEmailProvider(emailOptions.Provider);
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

    internal static string BuildAnalysisStoragePrefix(Guid analysisId)
    {
        return Program.BuildStoragePath("property-analyses", analysisId.ToString("D"));
    }

    internal static string BuildStoragePath(params string[] segments)
    {
        return string.Join(
            '/',
            segments
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .Select(segment => segment.Trim().Trim('/', '\\')));
    }

    internal static string CreateTemporaryFilePath(string? extension)
    {
        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? ".tmp"
            : extension.StartsWith('.') ? extension : $".{extension}";

        return Path.Combine(Path.GetTempPath(), $"propsegura-{Guid.NewGuid():N}{normalizedExtension}");
    }

    internal static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary file cleanup is best-effort.
        }
    }

    internal static async Task TryDeleteStoredFileAsync(
        IPropertyFileStorage fileStorage,
        string storagePath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await fileStorage.DeleteAsync(storagePath, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not clean up stored file after document upload failure. StoragePath={StoragePath} Exception={Exception}",
                storagePath,
                exception.ToString());
        }
    }

    internal static bool IsAllowedCoverImageContentType(string? contentType)
    {
        return string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "image/webp", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAllowedPdfContentType(string? contentType)
    {
        return string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAllowedDocumentImageContentType(string? contentType)
    {
        return string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAllowedDocumentUploadContentType(string? contentType)
    {
        return IsAllowedPdfContentType(contentType) || IsAllowedDocumentImageContentType(contentType);
    }

    internal static long GetMaxImageSizeBytes(StorageOptions options)
    {
        return options.MaxImageSizeBytes > 0 ? options.MaxImageSizeBytes : MaxCoverImageSizeBytes;
    }

    internal static long GetMaxPdfSizeBytes(StorageOptions options)
    {
        return options.MaxPdfSizeBytes > 0 ? options.MaxPdfSizeBytes : 15 * 1024 * 1024;
    }

    internal static string FormatMegabytes(long bytes)
    {
        var megabytes = bytes / 1024m / 1024m;
        return megabytes % 1 == 0
            ? megabytes.ToString("0")
            : megabytes.ToString("0.##");
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

    internal static string GetDocumentUploadExtension(string contentType, string? fileName)
    {
        var extension = Path.GetExtension(fileName);

        if (!string.IsNullOrWhiteSpace(extension))
        {
            extension = extension.ToLowerInvariant();

            if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) && extension == ".pdf")
            {
                return extension;
            }

            if (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) && extension is ".jpg" or ".jpeg")
            {
                return extension;
            }

            if (contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) && extension == ".png")
            {
                return extension;
            }
        }

        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => ".bin"
        };
    }

    internal static string GetFileExtensionFromStoragePath(string storagePath)
    {
        var extension = Path.GetExtension(storagePath);
        return string.IsNullOrWhiteSpace(extension) ? ".tmp" : extension;
    }

    internal static string BuildReportFileName(string title, DateTime generatedAtUtc)
    {
        var safeTitle = new string(title
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_' or ' ')
            .ToArray())
            .Trim()
            .Replace(' ', '-');

        if (string.IsNullOrWhiteSpace(safeTitle))
        {
            safeTitle = "Propiedad";
        }

        return $"PropSegura-{safeTitle}-{generatedAtUtc:yyyyMMdd}.pdf";
    }
}
