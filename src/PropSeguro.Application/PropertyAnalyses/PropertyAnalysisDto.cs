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
    string PropertyCondition,
    string? Notes,
    int OpportunityScore,
    int ProtectionScore,
    string InitialRiskLevel,
    string RecommendedUse,
    decimal? UsdPerSquareMeter,
    string BenchmarkComparison,
    string MarketPosition,
    decimal? EstimatedFairValueMinUsd,
    decimal? EstimatedFairValueMaxUsd,
    decimal? SuggestedFirstOfferUsd,
    decimal? SuggestedTargetPriceUsd,
    decimal? NegotiationMarginPercent,
    string NegotiationExplanation,
    NegotiationAnalysisDto NegotiationAnalysis,
    string Status,
    IReadOnlyCollection<RiskFindingDto> RiskFindings,
    IReadOnlyCollection<PropertyDocumentDto> Documents,
    IReadOnlyCollection<BuyerChecklistItemDto> ChecklistItems,
    IReadOnlyCollection<DocumentFindingDto> DocumentFindings,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PropertyAnalysisDocumentsDto(
    Guid PropertyAnalysisId,
    int CurrentProtectionScore,
    IReadOnlyCollection<PropertyDocumentDto> Documents,
    IReadOnlyCollection<BuyerChecklistItemDto> ChecklistItems);

public sealed record PropertyAnalysisSummaryDto(
    Guid Id,
    string Title,
    string? ListingUrl,
    string Address,
    string Neighborhood,
    decimal? PriceUsd,
    decimal? MonthlyExpenses,
    decimal? TotalSquareMeters,
    decimal? UsdPerSquareMeter,
    int OpportunityScore,
    int ProtectionScore,
    string RecommendedUse,
    string InitialRiskLevel,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int MissingRequiredDocumentsCount,
    int FindingsCount,
    int CriticalFindingsCount,
    NegotiationAnalysisDto NegotiationAnalysis);

public sealed record NegotiationAnalysisDto(
    decimal? PublishedPriceUsd,
    decimal? UsdPerSquareMeter,
    decimal? BenchmarkUsdPerSquareMeter,
    string MarketPosition,
    decimal? EstimatedFairValueMinUsd,
    decimal? EstimatedFairValueMaxUsd,
    decimal? SuggestedFirstOfferUsd,
    decimal? SuggestedTargetPriceUsd,
    decimal? NegotiationMarginPercent,
    string Explanation);

public sealed record DocumentAnalysisResultDto(
    Guid AnalysisId,
    Guid DocumentId,
    string AnalysisStatus,
    string? AnalysisSummary,
    string Summary,
    string RiskLevel,
    IReadOnlyCollection<DocumentFindingDto> Findings,
    IReadOnlyCollection<string> SuggestedQuestions,
    IReadOnlyCollection<string> SuggestedClauseChanges,
    string BuyerFriendlyExplanation,
    int CurrentProtectionScore);

public sealed record RiskFindingDto(
    Guid Id,
    string Type,
    string Severity,
    string Code,
    string Title,
    string Description);

public sealed record BuyerChecklistItemDto(
    Guid Id,
    string DocumentType,
    string Title,
    string Description,
    bool IsRequired,
    bool IsProvided,
    string Status);

public sealed record PropertyDocumentDto(
    Guid Id,
    string DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StoragePath,
    bool HasExtractedText,
    string? ExtractedText,
    string? EditedText,
    string TextExtractionStatus,
    DateTime UploadedAtUtc,
    string Status,
    string AnalysisStatus,
    DateTime? AnalyzedAtUtc,
    string? AnalysisSummary);

public sealed record DocumentFindingDto(
    Guid Id,
    Guid PropertyDocumentId,
    string Severity,
    string Category,
    string Title,
    string Description,
    string? SuggestedQuestion,
    string? SuggestedChange,
    DateTime CreatedAtUtc);

public static class PropertyAnalysisMapping
{
    public static PropertyAnalysisDto ToDto(this PropertyAnalysis analysis, IEnumerable<DocumentFinding>? documentFindings = null)
    {
        var findingDtos = (documentFindings ?? [])
            .OrderByDescending(finding => finding.Severity)
            .Select(finding => finding.ToDto())
            .ToArray();

        return new PropertyAnalysisDto(
            analysis.Id,
            analysis.Title,
            analysis.ListingUrl,
            analysis.Address,
            analysis.Neighborhood,
            analysis.PriceUsd,
            analysis.MonthlyExpenses,
            analysis.TotalSquareMeters,
            analysis.CoveredSquareMeters,
            analysis.Rooms,
            analysis.Bedrooms,
            analysis.Bathrooms,
            analysis.BuildingAgeYears,
            analysis.Floor,
            analysis.HasBalcony,
            analysis.HasAmenities,
            analysis.IsCreditEligible,
            analysis.PropertyCondition.ToString(),
            analysis.Notes,
            analysis.OpportunityScore,
            analysis.ProtectionScore,
            analysis.InitialRiskLevel.ToString(),
            analysis.RecommendedUse.ToString(),
            analysis.UsdPerSquareMeter,
            analysis.BenchmarkComparison,
            analysis.MarketPosition.ToString(),
            analysis.EstimatedFairValueMinUsd,
            analysis.EstimatedFairValueMaxUsd,
            analysis.SuggestedFirstOfferUsd,
            analysis.SuggestedTargetPriceUsd,
            analysis.NegotiationMarginPercent,
            analysis.NegotiationExplanation,
            analysis.ToNegotiationAnalysisDto(),
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
            analysis.Documents
                .OrderByDescending(document => document.UploadedAtUtc)
                .Select(document => new PropertyDocumentDto(
                    document.Id,
                    document.DocumentType.ToString(),
                    document.FileName,
                    document.ContentType,
                    document.SizeBytes,
                    document.StoragePath,
                    !string.IsNullOrWhiteSpace(document.ExtractedText),
                    document.ExtractedText,
                    document.EditedText,
                    document.TextExtractionStatus.ToString(),
                    document.UploadedAtUtc,
                    document.Status.ToString(),
                    document.AnalysisStatus.ToString(),
                    document.AnalyzedAtUtc,
                    document.AnalysisSummary))
                .ToArray(),
            analysis.ChecklistItems
                .OrderBy(item => item.DocumentType)
                .Select(item => new BuyerChecklistItemDto(
                    item.Id,
                    item.DocumentType.ToString(),
                    item.Title,
                    item.Description,
                    item.IsRequired,
                    item.IsProvided,
                    item.Status.ToString()))
                .ToArray(),
            findingDtos,
            analysis.CreatedAtUtc,
            analysis.UpdatedAtUtc);
    }

    public static PropertyAnalysisDocumentsDto ToDocumentsDto(this PropertyAnalysis analysis)
    {
        var dto = analysis.ToDto();

        return new PropertyAnalysisDocumentsDto(
            dto.Id,
            dto.ProtectionScore,
            dto.Documents,
            dto.ChecklistItems);
    }

    public static DocumentFindingDto ToDto(this DocumentFinding finding)
    {
        return new DocumentFindingDto(
            finding.Id,
            finding.PropertyDocumentId,
            finding.Severity.ToString(),
            finding.Category.ToString(),
            finding.Title,
            finding.Description,
            finding.SuggestedQuestion,
            finding.SuggestedChange,
            finding.CreatedAtUtc);
    }

    public static NegotiationAnalysisDto ToNegotiationAnalysisDto(this PropertyAnalysis analysis)
    {
        var benchmark = NeighborhoodBenchmark.Find(analysis.Neighborhood);

        return new NegotiationAnalysisDto(
            analysis.PriceUsd,
            analysis.UsdPerSquareMeter,
            benchmark?.AverageUsdPerSquareMeter,
            analysis.MarketPosition.ToString(),
            analysis.EstimatedFairValueMinUsd,
            analysis.EstimatedFairValueMaxUsd,
            analysis.SuggestedFirstOfferUsd,
            analysis.SuggestedTargetPriceUsd,
            analysis.NegotiationMarginPercent,
            analysis.NegotiationExplanation);
    }
}
