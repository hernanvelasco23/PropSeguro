using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MailKit.Security;
using PropSeguro.Api.Auth;
using PropSeguro.Api.Email;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;
using PropSeguro.Domain.Users;
using PropSeguro.Infrastructure.Persistence;

namespace PropSeguro.Domain.Tests;

public sealed class AuthEndpointTests
{
    [Fact]
    public async Task Login_with_valid_user_returns_token()
    {
        await using var factory = CreateFactory();
        var user = await CreateUserAsync(factory, "buyer@propsegura.local", "Buyer User", "Password123!", UserRole.User);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = "Password123!"
        });

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(json?["token"]?.GetValue<string>()));
        Assert.Equal(user.Email, json?["user"]?["email"]?.GetValue<string>());
    }

    [Fact]
    public async Task Login_with_invalid_password_fails()
    {
        await using var factory = CreateFactory();
        var user = await CreateUserAsync(factory, "buyer@propsegura.local", "Buyer User", "Password123!", UserRole.User);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = user.Email,
            password = "wrong"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_property_endpoints_require_authentication()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/property-analyses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_is_rate_limited_after_too_many_attempts()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        HttpResponseMessage? response = null;

        for (var index = 0; index < 6; index++)
        {
            response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "rate-limit-login@propsegura.local",
                password = "wrong"
            });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response?.StatusCode);
    }

    [Fact]
    public async Task Beta_access_request_is_rate_limited_after_too_many_attempts()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        HttpResponseMessage? response = null;

        for (var index = 0; index < 6; index++)
        {
            response = await client.PostAsJsonAsync("/api/beta-access-requests", new
            {
                fullName = "Comprador Beta",
                email = $"rate-limit-beta-{index}@propsegura.local",
                phone = "",
                intendedUse = "Comprar una propiedad",
                message = ""
            });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response?.StatusCode);
    }

    [Fact]
    public async Task Pdf_report_endpoint_requires_authentication()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/property-analyses/{Guid.NewGuid()}/report/pdf");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Listing_import_requires_authentication()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/listing-imports", new
        {
            listingUrl = "https://example.com/property"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cover_image_endpoint_requires_authentication()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/property-analyses/{Guid.NewGuid()}/cover-image");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Document_file_endpoint_requires_authentication()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/property-analyses/{Guid.NewGuid()}/documents/{Guid.NewGuid()}/file");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_is_disabled_in_production()
    {
        await using var factory = CreateFactory(environmentName: "Production");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Owner_can_download_pdf_report()
    {
        await using var factory = CreateFactory();
        var user = await CreateUserAsync(factory, "owner@propsegura.local", "Owner User", "Password123!", UserRole.User);
        var analysisId = await CreateAnalysisAsync(factory, user.Id, "Depto owner");
        using var client = factory.CreateClient();
        await LoginAsync(client, user.Email, "Password123!");

        var response = await client.GetAsync($"/api/property-analyses/{analysisId}/report/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Admin_can_download_any_pdf_report()
    {
        await using var factory = CreateFactory();
        var admin = await CreateUserAsync(factory, "admin-pdf@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        var owner = await CreateUserAsync(factory, "owner-pdf@propsegura.local", "Owner User", "Password123!", UserRole.User);
        var analysisId = await CreateAnalysisAsync(factory, owner.Id, "Depto ajeno");
        using var client = factory.CreateClient();
        await LoginAsync(client, admin.Email, "Password123!");

        var response = await client.GetAsync($"/api/property-analyses/{analysisId}/report/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task User_cannot_download_another_users_pdf_report()
    {
        await using var factory = CreateFactory();
        var owner = await CreateUserAsync(factory, "real-owner@propsegura.local", "Owner User", "Password123!", UserRole.User);
        var other = await CreateUserAsync(factory, "other@propsegura.local", "Other User", "Password123!", UserRole.User);
        var analysisId = await CreateAnalysisAsync(factory, owner.Id, "Depto privado");
        using var client = factory.CreateClient();
        await LoginAsync(client, other.Email, "Password123!");

        var response = await client.GetAsync($"/api/property-analyses/{analysisId}/report/pdf");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task User_cannot_get_or_update_another_users_analysis()
    {
        await using var factory = CreateFactory();
        var owner = await CreateUserAsync(factory, "owner-private@propsegura.local", "Owner User", "Password123!", UserRole.User);
        var other = await CreateUserAsync(factory, "other-private@propsegura.local", "Other User", "Password123!", UserRole.User);
        var analysisId = await CreateAnalysisAsync(factory, owner.Id, "Depto privado");
        using var client = factory.CreateClient();
        await LoginAsync(client, other.Email, "Password123!");

        var getResponse = await client.GetAsync($"/api/property-analyses/{analysisId}");
        var updateResponse = await client.PutAsJsonAsync($"/api/property-analyses/{analysisId}", new
        {
            title = "Intento de edicion",
            listingUrl = "https://example.com",
            address = "Av. Corrientes 1000",
            neighborhood = "Almagro",
            priceUsd = 90000,
            monthlyExpenses = 90000,
            totalSquareMeters = 45,
            coveredSquareMeters = 42,
            rooms = 2,
            bedrooms = 1,
            bathrooms = 1,
            buildingAgeYears = 12,
            floor = 4,
            apartmentUnit = "B",
            unitIdentifier = "4B",
            hasBalcony = true,
            hasAmenities = false,
            isCreditEligible = true,
            propertyCondition = "Good",
            notes = "No deberia guardarse"
        });

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
    }

    [Fact]
    public async Task User_cannot_upload_cover_image_to_another_users_analysis()
    {
        await using var factory = CreateFactory();
        var owner = await CreateUserAsync(factory, "cover-owner@propsegura.local", "Owner User", "Password123!", UserRole.User);
        var other = await CreateUserAsync(factory, "cover-other@propsegura.local", "Other User", "Password123!", UserRole.User);
        var analysisId = await CreateAnalysisAsync(factory, owner.Id, "Depto con portada");
        using var client = factory.CreateClient();
        await LoginAsync(client, other.Email, "Password123!");
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 137, 80, 78, 71 });
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "cover.png");

        var response = await client.PostAsync($"/api/property-analyses/{analysisId}/cover-image", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task User_cannot_access_another_users_cover_image()
    {
        await using var factory = CreateFactory();
        var owner = await CreateUserAsync(factory, "cover-read-owner@propsegura.local", "Owner User", "Password123!", UserRole.User);
        var other = await CreateUserAsync(factory, "cover-read-other@propsegura.local", "Other User", "Password123!", UserRole.User);
        var analysisId = await CreateAnalysisWithCoverAsync(factory, owner.Id, "Depto con portada privada");
        using var client = factory.CreateClient();
        await LoginAsync(client, other.Email, "Password123!");

        var response = await client.GetAsync($"/api/property-analyses/{analysisId}/cover-image");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task User_cannot_analyze_or_edit_document_from_another_users_analysis()
    {
        await using var factory = CreateFactory();
        var owner = await CreateUserAsync(factory, "document-owner@propsegura.local", "Owner User", "Password123!", UserRole.User);
        var other = await CreateUserAsync(factory, "document-other@propsegura.local", "Other User", "Password123!", UserRole.User);
        var (analysisId, documentId) = await CreateAnalysisWithDocumentAsync(factory, owner.Id, "Depto con reserva");
        using var client = factory.CreateClient();
        await LoginAsync(client, other.Email, "Password123!");

        var analyzeResponse = await client.PostAsJsonAsync($"/api/property-analyses/{analysisId}/documents/{documentId}/analyze", new
        {
            extractedText = "Reserva no reintegrable."
        });
        var textResponse = await client.PutAsJsonAsync($"/api/property-analyses/{analysisId}/documents/{documentId}/text", new
        {
            editedText = "Texto ajeno"
        });

        Assert.Equal(HttpStatusCode.NotFound, analyzeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, textResponse.StatusCode);
    }

    [Fact]
    public async Task User_cannot_access_another_users_document_file()
    {
        await using var factory = CreateFactory();
        var owner = await CreateUserAsync(factory, "document-file-owner@propsegura.local", "Owner User", "Password123!", UserRole.User);
        var other = await CreateUserAsync(factory, "document-file-other@propsegura.local", "Other User", "Password123!", UserRole.User);
        var (analysisId, documentId) = await CreateAnalysisWithDocumentAsync(factory, owner.Id, "Depto con documento privado");
        using var client = factory.CreateClient();
        await LoginAsync(client, other.Email, "Password123!");

        var response = await client.GetAsync($"/api/property-analyses/{analysisId}/documents/{documentId}/file");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Property_detail_response_does_not_expose_storage_paths()
    {
        await using var factory = CreateFactory();
        var owner = await CreateUserAsync(factory, "storage-path-owner@propsegura.local", "Owner User", "Password123!", UserRole.User);
        var (analysisId, _) = await CreateAnalysisWithDocumentAsync(factory, owner.Id, "Depto con documento privado");
        using var client = factory.CreateClient();
        await LoginAsync(client, owner.Email, "Password123!");

        var response = await client.GetAsync($"/api/property-analyses/{analysisId}");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("storagePath", json);
        Assert.DoesNotContain("coverImageStoragePath", json);
    }

    [Fact]
    public async Task Report_response_does_not_expose_cover_storage_path()
    {
        await using var factory = CreateFactory();
        var owner = await CreateUserAsync(factory, "report-storage-path-owner@propsegura.local", "Owner User", "Password123!", UserRole.User);
        var analysisId = await CreateAnalysisWithCoverAsync(factory, owner.Id, "Depto con portada privada");
        using var client = factory.CreateClient();
        await LoginAsync(client, owner.Email, "Password123!");

        var response = await client.GetAsync($"/api/property-analyses/{analysisId}/report");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("coverImageStoragePath", json);
    }

    [Fact]
    public async Task User_only_sees_own_analyses()
    {
        await using var factory = CreateFactory();
        var firstUser = await CreateUserAsync(factory, "first@propsegura.local", "First User", "Password123!", UserRole.User);
        var secondUser = await CreateUserAsync(factory, "second@propsegura.local", "Second User", "Password123!", UserRole.User);
        await CreateAnalysisAsync(factory, firstUser.Id, "Depto propio");
        await CreateAnalysisAsync(factory, secondUser.Id, "Depto ajeno");
        using var client = factory.CreateClient();
        await LoginAsync(client, firstUser.Email, "Password123!");

        var result = await client.GetFromJsonAsync<JsonArray>("/api/property-analyses");

        var item = Assert.Single(result!);
        Assert.Equal("Depto propio", item?["title"]?.GetValue<string>());
    }

    [Fact]
    public async Task Admin_sees_all_analyses()
    {
        await using var factory = CreateFactory();
        var admin = await CreateUserAsync(factory, "admin-test@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        var user = await CreateUserAsync(factory, "buyer@propsegura.local", "Buyer User", "Password123!", UserRole.User);
        await CreateAnalysisAsync(factory, admin.Id, "Depto admin");
        await CreateAnalysisAsync(factory, user.Id, "Depto user");
        using var client = factory.CreateClient();
        await LoginAsync(client, admin.Email, "Password123!");

        var result = await client.GetFromJsonAsync<JsonArray>("/api/property-analyses");

        Assert.Equal(2, result!.Count);
    }

    [Fact]
    public async Task Admin_endpoints_reject_non_admin_users()
    {
        await using var factory = CreateFactory();
        var user = await CreateUserAsync(factory, "regular@propsegura.local", "Regular User", "Password123!", UserRole.User);
        using var client = factory.CreateClient();
        await LoginAsync(client, user.Email, "Password123!");

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_access_admin_endpoints_with_admin_jwt()
    {
        await using var factory = CreateFactory();
        var admin = await CreateUserAsync(factory, "admin-access@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        using var client = factory.CreateClient();
        await LoginAsync(client, admin.Email, "Password123!");

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_user_creation_rejects_weak_passwords()
    {
        await using var factory = CreateFactory();
        var admin = await CreateUserAsync(factory, "admin-password@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        using var client = factory.CreateClient();
        await LoginAsync(client, admin.Email, "Password123!");

        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            email = "weak@propsegura.local",
            fullName = "Weak Password",
            password = "short",
            role = "User"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Beta_access_request_can_be_created_publicly()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/beta-access-requests", new
        {
            fullName = "Comprador Beta",
            email = "beta@propsegura.local",
            phone = "1111-2222",
            intendedUse = "Estoy evaluando una propiedad.",
            message = "Quiero probar la beta."
        });

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Pending", json?["status"]?.GetValue<string>());
    }

    [Fact]
    public async Task Admin_can_approve_beta_request_create_invited_user_and_send_invitation()
    {
        var invitationEmailService = new RecordingBetaInvitationEmailService();
        await using var factory = CreateFactory(invitationEmailService, useSmtpEmailProvider: true);
        var admin = await CreateUserAsync(factory, "admin-test@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        using var client = factory.CreateClient();
        var requestResponse = await client.PostAsJsonAsync("/api/beta-access-requests", new
        {
            fullName = "Comprador Beta",
            email = "beta@propsegura.local",
            phone = "",
            intendedUse = "Comprar una propiedad",
            message = ""
        });
        var requestJson = await requestResponse.Content.ReadFromJsonAsync<JsonObject>();
        await LoginAsync(client, admin.Email, "Password123!");

        var approveResponse = await client.PostAsync($"/api/admin/beta-access-requests/{requestJson?["id"]?.GetValue<Guid>()}/approve", null);
        var approveJson = await approveResponse.Content.ReadFromJsonAsync<JsonObject>();

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Equal("Approved", approveJson?["status"]?.GetValue<string>());
        Assert.True(approveJson?["emailSent"]?.GetValue<bool>());
        Assert.Equal("beta@propsegura.local", approveJson?["email"]?.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(approveJson?["temporaryPassword"]?.GetValue<string>()));
        Assert.Equal(1, invitationEmailService.SendCount);
        Assert.Equal("beta@propsegura.local", invitationEmailService.LastEmail);
        Assert.False(string.IsNullOrWhiteSpace(invitationEmailService.LastTemporaryPassword));
        Assert.Equal("http://localhost:5173/login", invitationEmailService.LastLoginUrl);
    }

    [Fact]
    public async Task Beta_invitation_email_contains_login_url_email_and_temporary_password()
    {
        var emailSender = new RecordingEmailSender();
        var service = new BetaInvitationEmailService(
            emailSender,
            Options.Create(new EmailOptions
            {
                FromName = "PropSegura"
            }));
        var user = new AppUser("invited@propsegura.local", "Invited User", "hash", UserRole.User, UserStatus.Invited);

        await service.SendInvitationAsync(user, "Temp-1234", "https://propsegura.com.ar/login", CancellationToken.None);

        Assert.NotNull(emailSender.LastMessage);
        Assert.Equal("invited@propsegura.local", emailSender.LastMessage!.ToEmail);
        Assert.Equal("Tu acceso a PropSegura Beta fue aprobado", emailSender.LastMessage.Subject);
        Assert.Contains("https://propsegura.com.ar/login", emailSender.LastMessage.HtmlBody);
        Assert.Contains("invited@propsegura.local", emailSender.LastMessage.HtmlBody);
        Assert.Contains("Temp-1234", emailSender.LastMessage.HtmlBody);
        Assert.Contains("PropSegura no reemplaza", emailSender.LastMessage.HtmlBody);
        Assert.Contains("https://propsegura.com.ar/login", emailSender.LastMessage.TextBody);
        Assert.Contains("invited@propsegura.local", emailSender.LastMessage.TextBody);
        Assert.Contains("Temp-1234", emailSender.LastMessage.TextBody);
    }

    [Fact]
    public async Task Null_email_provider_approval_returns_temporary_password_without_sending_email()
    {
        var invitationEmailService = new RecordingBetaInvitationEmailService();
        await using var factory = CreateFactory(invitationEmailService);
        var admin = await CreateUserAsync(factory, "admin-null-email@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        using var client = factory.CreateClient();
        var requestId = await CreateBetaAccessRequestAsync(client, "null-email-user@propsegura.local");
        await LoginAsync(client, admin.Email, "Password123!");

        var approveResponse = await client.PostAsync($"/api/admin/beta-access-requests/{requestId}/approve", null);
        var approveJson = await approveResponse.Content.ReadFromJsonAsync<JsonObject>();

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.False(approveJson?["emailSent"]?.GetValue<bool>());
        Assert.Equal(
            "Email deshabilitado en este entorno. Copiá la contraseña temporal manualmente.",
            approveJson?["message"]?.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(approveJson?["temporaryPassword"]?.GetValue<string>()));
        Assert.Equal(0, invitationEmailService.SendCount);
    }

    [Fact]
    public async Task Approving_beta_request_returns_email_sent_false_when_email_fails()
    {
        var invitationEmailService = new RecordingBetaInvitationEmailService { ThrowOnSend = true };
        await using var factory = CreateFactory(invitationEmailService, useSmtpEmailProvider: true);
        var admin = await CreateUserAsync(factory, "admin-email-fail@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        using var client = factory.CreateClient();
        var requestId = await CreateBetaAccessRequestAsync(client, "email-fail@propsegura.local");
        await LoginAsync(client, admin.Email, "Password123!");

        var approveResponse = await client.PostAsync($"/api/admin/beta-access-requests/{requestId}/approve", null);
        var approveJson = await approveResponse.Content.ReadFromJsonAsync<JsonObject>();

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.False(approveJson?["emailSent"]?.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(approveJson?["emailError"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(approveJson?["temporaryPassword"]?.GetValue<string>()));
    }

    [Fact]
    public async Task Production_approval_with_sent_email_does_not_return_temporary_password()
    {
        var invitationEmailService = new RecordingBetaInvitationEmailService();
        await using var factory = CreateFactory(
            invitationEmailService,
            useSmtpEmailProvider: true,
            environmentName: "Production");
        var admin = await CreateUserAsync(factory, "admin-prod-approval@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        using var client = factory.CreateClient();
        var requestId = await CreateBetaAccessRequestAsync(client, "prod-approved@propsegura.local");
        await LoginAsync(client, admin.Email, "Password123!");

        var approveResponse = await client.PostAsync($"/api/admin/beta-access-requests/{requestId}/approve", null);
        var approveJson = await approveResponse.Content.ReadFromJsonAsync<JsonObject>();

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.True(approveJson?["emailSent"]?.GetValue<bool>());
        Assert.Null(approveJson?["temporaryPassword"]);
        Assert.Equal(1, invitationEmailService.SendCount);
    }

    [Fact]
    public async Task Approving_beta_request_stores_hashed_temporary_password()
    {
        await using var factory = CreateFactory();
        var admin = await CreateUserAsync(factory, "admin-hash@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        using var client = factory.CreateClient();
        var requestId = await CreateBetaAccessRequestAsync(client, "hash-user@propsegura.local");
        await LoginAsync(client, admin.Email, "Password123!");

        var approveResponse = await client.PostAsync($"/api/admin/beta-access-requests/{requestId}/approve", null);
        var approveJson = await approveResponse.Content.ReadFromJsonAsync<JsonObject>();
        var temporaryPassword = approveJson?["temporaryPassword"]?.GetValue<string>();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = await dbContext.Users.SingleAsync(item => item.Email == "hash-user@propsegura.local");

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(temporaryPassword));
        Assert.NotEqual(temporaryPassword, user.PasswordHash);
        Assert.True(passwordHasher.Verify(temporaryPassword!, user.PasswordHash));
    }

    [Fact]
    public async Task Non_admin_cannot_approve_beta_request()
    {
        await using var factory = CreateFactory();
        var user = await CreateUserAsync(factory, "not-admin-approve@propsegura.local", "Regular User", "Password123!", UserRole.User);
        using var client = factory.CreateClient();
        var requestId = await CreateBetaAccessRequestAsync(client, "pending-approve@propsegura.local");
        await LoginAsync(client, user.Email, "Password123!");

        var response = await client.PostAsync($"/api/admin/beta-access-requests/{requestId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approving_beta_request_for_existing_user_does_not_create_duplicate_user()
    {
        var invitationEmailService = new RecordingBetaInvitationEmailService();
        await using var factory = CreateFactory(invitationEmailService, useSmtpEmailProvider: true);
        var admin = await CreateUserAsync(factory, "admin-duplicate@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        await CreateUserAsync(factory, "existing@propsegura.local", "Existing User", "Password123!", UserRole.User);
        using var client = factory.CreateClient();
        var requestId = await CreateBetaAccessRequestAsync(client, "existing@propsegura.local");
        await LoginAsync(client, admin.Email, "Password123!");

        var approveResponse = await client.PostAsync($"/api/admin/beta-access-requests/{requestId}/approve", null);
        var approveJson = await approveResponse.Content.ReadFromJsonAsync<JsonObject>();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userCount = await dbContext.Users.CountAsync(item => item.Email == "existing@propsegura.local");

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Equal(1, userCount);
        Assert.Null(approveJson?["temporaryPassword"]);
        Assert.Equal(0, invitationEmailService.SendCount);
    }

    [Fact]
    public async Task Approving_beta_request_for_existing_invited_user_sends_new_invitation_without_duplicate_user()
    {
        var invitationEmailService = new RecordingBetaInvitationEmailService();
        await using var factory = CreateFactory(invitationEmailService, useSmtpEmailProvider: true);
        var admin = await CreateUserAsync(factory, "admin-existing-invited@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        await CreateUserAsync(factory, "existing-invited@propsegura.local", "Existing Invited", "OldPassword123!", UserRole.User, UserStatus.Invited);
        using var client = factory.CreateClient();
        var requestId = await CreateBetaAccessRequestAsync(client, "existing-invited@propsegura.local");
        await LoginAsync(client, admin.Email, "Password123!");

        var approveResponse = await client.PostAsync($"/api/admin/beta-access-requests/{requestId}/approve", null);
        var approveJson = await approveResponse.Content.ReadFromJsonAsync<JsonObject>();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userCount = await dbContext.Users.CountAsync(item => item.Email == "existing-invited@propsegura.local");

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Equal(1, userCount);
        Assert.True(approveJson?["emailSent"]?.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(approveJson?["temporaryPassword"]?.GetValue<string>()));
        Assert.Equal(1, invitationEmailService.SendCount);
    }

    [Fact]
    public async Task Approving_already_approved_request_returns_clear_message_without_second_invitation()
    {
        var invitationEmailService = new RecordingBetaInvitationEmailService();
        await using var factory = CreateFactory(invitationEmailService, useSmtpEmailProvider: true);
        var admin = await CreateUserAsync(factory, "admin-already-approved@propsegura.local", "Admin User", "Password123!", UserRole.Admin);
        using var client = factory.CreateClient();
        var requestId = await CreateBetaAccessRequestAsync(client, "already-approved@propsegura.local");
        await LoginAsync(client, admin.Email, "Password123!");

        var firstApproveResponse = await client.PostAsync($"/api/admin/beta-access-requests/{requestId}/approve", null);
        var secondApproveResponse = await client.PostAsync($"/api/admin/beta-access-requests/{requestId}/approve", null);
        var secondApproveJson = await secondApproveResponse.Content.ReadFromJsonAsync<JsonObject>();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userCount = await dbContext.Users.CountAsync(item => item.Email == "already-approved@propsegura.local");

        Assert.Equal(HttpStatusCode.OK, firstApproveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondApproveResponse.StatusCode);
        Assert.Equal(1, userCount);
        Assert.Equal(1, invitationEmailService.SendCount);
        Assert.False(secondApproveJson?["emailSent"]?.GetValue<bool>());
        Assert.Equal("La solicitud ya estaba aprobada.", secondApproveJson?["message"]?.GetValue<string>());
        Assert.Null(secondApproveJson?["temporaryPassword"]);
    }

    [Fact]
    public async Task Null_email_sender_does_not_throw()
    {
        var sender = new NullEmailSender(NullLogger<NullEmailSender>.Instance);

        await sender.SendAsync(new EmailMessage(
            "buyer@propsegura.local",
            "Buyer",
            "Test",
            "<p>Test</p>",
            "Test"), CancellationToken.None);
    }

    [Fact]
    public async Task Disabled_user_cannot_login()
    {
        await using var factory = CreateFactory();
        await CreateUserAsync(factory, "disabled@propsegura.local", "Disabled User", "Password123!", UserRole.User, UserStatus.Disabled);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "disabled@propsegura.local",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Disabled_user_token_is_rejected_even_if_token_was_issued()
    {
        await using var factory = CreateFactory();
        var user = await CreateUserAsync(factory, "disabled-token@propsegura.local", "Disabled Token", "Password123!", UserRole.User, UserStatus.Disabled);
        using var client = factory.CreateClient();
        var tokenService = factory.Services.GetRequiredService<IJwtTokenService>();
        var token = tokenService.CreateToken(user);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/property-analyses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Smtp_sender_uses_ssl_on_connect_for_donweb_port_465()
    {
        var socketOptions = SmtpEmailSender.ResolveSocketOptions(new SmtpEmailOptions
        {
            Host = "a0140791.ferozo.com",
            Port = 465,
            Username = "hola@propsegura.com.ar",
            Password = "test-password",
            EnableSsl = true
        });

        Assert.Equal(SecureSocketOptions.SslOnConnect, socketOptions);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IBetaInvitationEmailService? invitationEmailService = null,
        bool useSmtpEmailProvider = false,
        string environmentName = "Development")
    {
        var databaseName = Guid.NewGuid().ToString("N");

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environmentName);
                builder.UseSetting("Jwt:Issuer", "PropSegura.Tests");
                builder.UseSetting("Jwt:Audience", "PropSegura.Tests");
                builder.UseSetting("Jwt:SigningKey", "propsegura-tests-signing-key-with-more-than-32-bytes");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(localdb)\\mssqllocaldb;Database=PropSeguroTests;Trusted_Connection=True;TrustServerCertificate=True");
                builder.UseSetting("Storage:Provider", "Local");
                builder.UseSetting("Email:Provider", useSmtpEmailProvider ? "Smtp" : "Null");
                builder.UseSetting("Email:FromEmail", "no-reply@propsegura.local");
                builder.UseSetting("Email:FromName", "PropSegura");
                builder.UseSetting("Email:Smtp:Host", "smtp.test.local");
                builder.UseSetting("Email:Smtp:Port", "587");
                builder.UseSetting("Email:Smtp:Username", "smtp-user");
                builder.UseSetting("Email:Smtp:Password", "smtp-secret");
                builder.UseSetting("Email:Smtp:EnableSsl", "true");
                builder.UseSetting("Frontend:BaseUrl", "http://localhost:5173");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    var emailSettings = new Dictionary<string, string?>
                    {
                        ["Jwt:Issuer"] = "PropSegura.Tests",
                        ["Jwt:Audience"] = "PropSegura.Tests",
                        ["Jwt:SigningKey"] = "propsegura-tests-signing-key-with-more-than-32-bytes",
                        ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=PropSeguroTests;Trusted_Connection=True;TrustServerCertificate=True",
                        ["Storage:Provider"] = "Local",
                        ["Email:Provider"] = useSmtpEmailProvider ? "Smtp" : "Null",
                        ["Email:FromEmail"] = "no-reply@propsegura.local",
                        ["Email:FromName"] = "PropSegura",
                        ["Email:Smtp:Host"] = "smtp.test.local",
                        ["Email:Smtp:Port"] = "587",
                        ["Email:Smtp:Username"] = "smtp-user",
                        ["Email:Smtp:Password"] = "smtp-password",
                        ["Email:Smtp:EnableSsl"] = "true",
                        ["Frontend:BaseUrl"] = "http://localhost:5173"
                    };

                    configurationBuilder.AddInMemoryCollection(emailSettings);
                });

                builder.ConfigureServices(services =>
                {
                    var dbContextOptions = services.SingleOrDefault(item => item.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (dbContextOptions is not null)
                    {
                        services.Remove(dbContextOptions);
                    }

                    var appDbContext = services.SingleOrDefault(item => item.ServiceType == typeof(IApplicationDbContext));
                    if (appDbContext is not null)
                    {
                        services.Remove(appDbContext);
                    }

                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                    services.AddScoped<IApplicationDbContext>(provider =>
                        provider.GetRequiredService<ApplicationDbContext>());
                    services
                        .AddDataProtection()
                        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "propsegura-test-keys")));

                    if (invitationEmailService is not null)
                    {
                        services.RemoveAll<IBetaInvitationEmailService>();
                        services.AddSingleton(invitationEmailService);
                    }
                });
            });
    }

    private static async Task<Guid> CreateBetaAccessRequestAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/beta-access-requests", new
        {
            fullName = "Comprador Beta",
            email,
            phone = "",
            intendedUse = "Comprar una propiedad",
            message = ""
        });
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();

        return json?["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("Beta access request was not created.");
    }

    private static async Task<AppUser> CreateUserAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string fullName,
        string password,
        UserRole role,
        UserStatus status = UserStatus.Active)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = new AppUser(email, fullName, passwordHasher.Hash(password), role, status);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
    }

    private static async Task<Guid> CreateAnalysisAsync(WebApplicationFactory<Program> factory, Guid userId, string title)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var analysis = new PropertyAnalysis(
            title,
            "https://example.com",
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
            PropertyCondition.Good,
            null);
        analysis.AssignOwner(userId);
        analysis.RunInitialAnalysis();
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();
        return analysis.Id;
    }

    private static async Task<(Guid AnalysisId, Guid DocumentId)> CreateAnalysisWithDocumentAsync(
        WebApplicationFactory<Program> factory,
        Guid userId,
        string title)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var analysis = new PropertyAnalysis(
            title,
            "https://example.com",
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
            PropertyCondition.Good,
            null);
        analysis.AssignOwner(userId);
        analysis.RunInitialAnalysis();
        var documentId = Guid.NewGuid();
        analysis.AddDocument(documentId, DocumentType.ReservationAgreement, "reserva.pdf", "application/pdf", 100, "storage/test/reserva.pdf");
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();
        return (analysis.Id, documentId);
    }

    private static async Task<Guid> CreateAnalysisWithCoverAsync(
        WebApplicationFactory<Program> factory,
        Guid userId,
        string title)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var analysis = new PropertyAnalysis(
            title,
            "https://example.com",
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
            PropertyCondition.Good,
            null);
        analysis.AssignOwner(userId);
        analysis.RunInitialAnalysis();
        analysis.SetCoverImage("cover.jpg", "image/jpeg", 100, $"property-analyses/{analysis.Id:D}/cover/cover.jpg");
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();
        return analysis.Id;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var token = json?["token"]?.GetValue<string>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private sealed class RecordingBetaInvitationEmailService : IBetaInvitationEmailService
    {
        public bool ThrowOnSend { get; init; }

        public int SendCount { get; private set; }

        public string? LastEmail { get; private set; }

        public string? LastTemporaryPassword { get; private set; }

        public string? LastLoginUrl { get; private set; }

        public Task SendInvitationAsync(AppUser user, string temporaryPassword, string loginUrl, CancellationToken cancellationToken)
        {
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("SMTP failure.");
            }

            SendCount++;
            LastEmail = user.Email;
            LastTemporaryPassword = temporaryPassword;
            LastLoginUrl = loginUrl;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public EmailMessage? LastMessage { get; private set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }
    }
}
