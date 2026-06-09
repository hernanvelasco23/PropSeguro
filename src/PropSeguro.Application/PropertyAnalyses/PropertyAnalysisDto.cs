using PropSeguro.Application.Common;
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
    string? ApartmentUnit,
    string? UnitIdentifier,
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
    string? CoverImageFileName,
    string? CoverImageContentType,
    long? CoverImageSizeBytes,
    string? CoverImageStoragePath,
    DateTime? CoverImageUploadedAtUtc,
    string? CoverImageUrl,
    string Status,
    IReadOnlyCollection<RiskFindingDto> RiskFindings,
    IReadOnlyCollection<PropertyDocumentDto> Documents,
    IReadOnlyCollection<BuyerChecklistItemDto> ChecklistItems,
    IReadOnlyCollection<DocumentFindingDto> DocumentFindings,
    ExpenseIntelligenceAnalysisDto? ExpenseIntelligence,
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
    string? CoverImageUrl,
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
    IReadOnlyCollection<string> SuggestedNegotiationPoints,
    ExpenseIntelligenceAnalysisDto? ExpenseIntelligence,
    ExpenseDocumentValidationDto? ExpenseValidation,
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
    string TextExtractionQuality,
    string? LastAnalyzedTextSource,
    DateTime UploadedAtUtc,
    string Status,
    string AnalysisStatus,
    DateTime? AnalyzedAtUtc,
    string? AnalysisSummary,
    ExpenseDocumentValidationDto? ExpenseValidation);

public sealed record DocumentFindingDto(
    Guid Id,
    Guid PropertyDocumentId,
    string Severity,
    string Category,
    string Title,
    string Description,
    string? BuyerImpact,
    string? SuggestedQuestion,
    string? SuggestedChange,
    string? SuggestedNegotiationPoint,
    string? MatchedText,
    DateTime CreatedAtUtc);

public sealed record ExpenseIntelligenceAnalysisDto(
    int BuildingHealthScore,
    string Classification,
    string Summary,
    IReadOnlyCollection<DocumentFindingDto> Findings,
    IReadOnlyCollection<string> SuggestedQuestions,
    IReadOnlyCollection<string> SuggestedNegotiationPoints);

public sealed record ExpenseDocumentValidationDto(
    int? DetectedFloor,
    string? DetectedApartmentUnit,
    string? DetectedUnitIdentifier,
    decimal? DetectedExpenseAmount,
    string ExtractionConfidence,
    int ConfidenceScore,
    string Classification,
    string Label,
    string Explanation);

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
            analysis.ApartmentUnit,
            analysis.UnitIdentifier,
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
            analysis.CoverImageFileName,
            analysis.CoverImageContentType,
            analysis.CoverImageSizeBytes,
            analysis.CoverImageStoragePath,
            analysis.CoverImageUploadedAtUtc,
            analysis.GetCoverImageUrl(),
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
                    document.TextExtractionQuality.ToString(),
                    document.LastAnalyzedTextSource?.ToString(),
                    document.UploadedAtUtc,
                    document.Status.ToString(),
                    document.AnalysisStatus.ToString(),
                    document.AnalyzedAtUtc,
                    document.AnalysisSummary,
                    document.DocumentType == DocumentType.Expenses
                        ? ExpenseDocumentValidator.Validate(
                            analysis,
                            document,
                            document.EditedText ?? document.ExtractedText).ToDto()
                        : null))
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
            analysis.ToExpenseIntelligenceAnalysisDto(documentFindings ?? []),
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
            finding.BuyerImpact,
            finding.SuggestedQuestion,
            finding.SuggestedChange,
            finding.SuggestedNegotiationPoint,
            finding.MatchedText,
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

    public static string? GetCoverImageUrl(this PropertyAnalysis analysis)
    {
        return string.IsNullOrWhiteSpace(analysis.CoverImageStoragePath)
            ? null
            : $"/api/property-analyses/{analysis.Id:D}/cover-image";
    }

    public static ExpenseIntelligenceAnalysisDto? ToExpenseIntelligenceAnalysisDto(
        this PropertyAnalysis analysis,
        IEnumerable<DocumentFinding> documentFindings)
    {
        var expenseDocumentIds = analysis.Documents
            .Where(document => document.DocumentType == DocumentType.Expenses)
            .Select(document => document.Id)
            .ToHashSet();

        if (expenseDocumentIds.Count == 0)
        {
            return null;
        }

        var expenseFindings = documentFindings
            .Where(finding => expenseDocumentIds.Contains(finding.PropertyDocumentId))
            .Where(finding => finding.Category != FindingCategory.Documentation)
            .OrderByDescending(finding => finding.Severity)
            .ToArray();
        var score = DeterministicExpenseIntelligenceAnalyzer.CalculateBuildingHealthScore(expenseFindings);

        return new ExpenseIntelligenceAnalysisDto(
            score,
            DeterministicExpenseIntelligenceAnalyzer.Classify(score),
            BuildExpenseSummary(expenseFindings),
            expenseFindings.Select(finding => finding.ToDto()).ToArray(),
            expenseFindings
                .Select(finding => finding.SuggestedQuestion)
                .OfType<string>()
                .Where(question => !string.IsNullOrWhiteSpace(question))
                .Distinct()
                .ToArray(),
            expenseFindings
                .Select(finding => finding.SuggestedNegotiationPoint ?? finding.SuggestedChange)
                .OfType<string>()
                .Where(point => !string.IsNullOrWhiteSpace(point))
                .Distinct()
                .ToArray());
    }

    public static ExpenseIntelligenceAnalysisDto? ToDto(this ExpenseIntelligenceAnalysis? analysis)
    {
        if (analysis is null)
        {
            return null;
        }

        return new ExpenseIntelligenceAnalysisDto(
            analysis.BuildingHealthScore,
            analysis.Classification,
            analysis.Summary,
            analysis.Findings.Select(finding => finding.ToDto()).ToArray(),
            analysis.SuggestedQuestions,
            analysis.SuggestedNegotiationPoints);
    }

    public static ExpenseDocumentValidationDto? ToDto(this ExpenseDocumentValidation? validation)
    {
        if (validation is null)
        {
            return null;
        }

        return new ExpenseDocumentValidationDto(
            validation.Extraction.DetectedFloor,
            validation.Extraction.DetectedApartmentUnit,
            validation.Extraction.DetectedUnitIdentifier,
            validation.Extraction.DetectedExpenseAmount,
            validation.Extraction.Confidence,
            validation.ConfidenceScore,
            validation.Classification,
            validation.Label,
            validation.Explanation);
    }

    private static string BuildExpenseSummary(IReadOnlyCollection<DocumentFinding> findings)
    {
        if (findings.Count == 0)
        {
            return "No se detectaron senales relevantes en las expensas con las reglas actuales.";
        }

        return $"Se detectaron {findings.Count} senal(es) en expensas para preguntar o negociar antes de avanzar.";
    }
}
