using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.PropertyAnalyses;

public sealed record PropertyAnalysisDto(
    Guid Id,
    string Title,
    string? ListingUrl,
    string Address,
    string Neighborhood,
    decimal? PriceUsd,
    decimal? MonthlyExpenses,
    string? Notes,
    int RiskScore,
    string Status,
    IReadOnlyCollection<RiskFindingDto> RiskFindings,
    IReadOnlyCollection<BuyerChecklistItemDto> MissingDocumentationChecklist,
    DateTime CreatedAtUtc);

public sealed record RiskFindingDto(
    Guid Id,
    string Type,
    string Severity,
    string Code,
    string Title,
    string Description);

public sealed record BuyerChecklistItemDto(
    Guid Id,
    string Title,
    string Category,
    bool IsCompleted);

public static class PropertyAnalysisMapping
{
    public static PropertyAnalysisDto ToDto(this PropertyAnalysis analysis)
    {
        return new PropertyAnalysisDto(
            analysis.Id,
            analysis.Title,
            analysis.ListingUrl,
            analysis.Address,
            analysis.Neighborhood,
            analysis.PriceUsd,
            analysis.MonthlyExpenses,
            analysis.Notes,
            analysis.RiskScore,
            analysis.Status.ToString(),
            analysis.RiskFindings
                .OrderByDescending(finding => finding.Severity)
                .Select(finding => new RiskFindingDto(
                    finding.Id,
                    finding.Type.ToString(),
                    finding.Severity.ToString(),
                    finding.Code,
                    finding.Title,
                    finding.Description))
                .ToArray(),
            analysis.ChecklistItems
                .Select(item => new BuyerChecklistItemDto(
                    item.Id,
                    item.Title,
                    item.Category.ToString(),
                    item.IsCompleted))
                .ToArray(),
            analysis.CreatedAtUtc);
    }
}
