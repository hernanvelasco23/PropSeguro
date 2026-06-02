using MediatR;
using PropSeguro.Application;
using PropSeguro.Application.PropertyAnalyses.CreatePropertyAnalysis;
using PropSeguro.Application.PropertyAnalyses.GetPropertyAnalysis;
using PropSeguro.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var propertyAnalyses = app.MapGroup("/api/property-analyses")
    .WithTags("Property Analyses");

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

propertyAnalyses.MapGet("/{id:guid}", async (
    Guid id,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetPropertyAnalysisQuery(id), cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
})
.WithName("GetPropertyAnalysis");

app.Run();

public sealed record CreatePropertyAnalysisRequest(
    string Title,
    string? ListingUrl,
    string Address,
    string Neighborhood,
    decimal? PriceUsd,
    decimal? MonthlyExpenses,
    string? Notes);
