namespace PropSeguro.Application.Common;

public interface IListingImporter
{
    Task<ListingImportResult> ImportAsync(string listingUrl, CancellationToken cancellationToken = default);
}

public sealed record ListingImportResult(
    string? Title,
    decimal? PriceUsd,
    decimal? MonthlyExpenses,
    string? Address,
    string? Neighborhood,
    decimal? TotalSquareMeters,
    decimal? CoveredSquareMeters,
    int? Rooms,
    int? Bedrooms,
    int? Bathrooms,
    string? Description,
    IReadOnlyCollection<string> ImageUrls,
    string SourceUrl,
    int ConfidenceScore,
    IReadOnlyCollection<string> Warnings);
