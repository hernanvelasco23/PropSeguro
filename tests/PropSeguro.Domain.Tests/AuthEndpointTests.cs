using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PropSeguro.Api.Auth;
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
    public async Task Admin_can_approve_beta_request_and_create_invited_user()
    {
        await using var factory = CreateFactory();
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
        Assert.Equal("Approved", approveJson?["request"]?["status"]?.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(approveJson?["temporaryPassword"]?.GetValue<string>()));
        Assert.Equal("Invited", approveJson?["user"]?["status"]?.GetValue<string>());
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

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
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
                });
            });
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

    private static async Task CreateAnalysisAsync(WebApplicationFactory<Program> factory, Guid userId, string title)
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
}
