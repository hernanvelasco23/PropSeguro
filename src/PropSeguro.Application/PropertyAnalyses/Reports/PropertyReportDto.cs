namespace PropSeguro.Application.PropertyAnalyses.Reports;

public sealed record PropertyReportDto(
    PropertyReportSummaryDto PropertySummary,
    string ExecutiveSummary,
    string OverallRecommendation,
    string OverallRecommendationLabel,
    string OverallRecommendationExplanation,
    PropertyReportOpportunitySectionDto OpportunitySection,
    PropertyReportProtectionSectionDto ProtectionSection,
    PropertyReportNegotiationSectionDto NegotiationSection,
    PropertyReportDocumentsSectionDto DocumentsSection,
    PropertyReportDocumentTypeSectionDto ReservationSection,
    PropertyReportExpensesSectionDto ExpensesSection,
    PropertyReportFindingsSectionDto FindingsSection,
    IReadOnlyCollection<string> SuggestedQuestions,
    IReadOnlyCollection<string> SuggestedNegotiationPoints,
    IReadOnlyCollection<string> NextSteps,
    string Disclaimer,
    DateTime GeneratedAtUtc);

public sealed record PropertyReportSummaryDto(
    string Title,
    string Address,
    string Neighborhood,
    decimal? PriceUsd,
    decimal? TotalSquareMeters,
    decimal? UsdPerSquareMeter,
    decimal? MonthlyExpenses,
    int? Floor,
    string? ApartmentUnit,
    string? UnitIdentifier);

public sealed record PropertyReportOpportunitySectionDto(
    int OpportunityScore,
    string RecommendedUse,
    string RecommendedUseLabel,
    string InitialRiskLevel,
    string InitialRiskLevelLabel,
    string BenchmarkComparison,
    string Explanation);

public sealed record PropertyReportProtectionSectionDto(
    int ProtectionScore,
    string DocumentConfidenceSummary,
    int MissingDocumentsCount,
    int HighOrCriticalFindingsCount,
    string Explanation);

public sealed record PropertyReportNegotiationSectionDto(
    decimal? PublishedPriceUsd,
    decimal? EstimatedFairValueMinUsd,
    decimal? EstimatedFairValueMaxUsd,
    decimal? SuggestedFirstOfferUsd,
    decimal? SuggestedTargetPriceUsd,
    decimal? NegotiationMarginPercent,
    string MarketPosition,
    string MarketPositionLabel,
    string Explanation);

public sealed record PropertyReportDocumentsSectionDto(
    int RequiredDocumentsCount,
    int ProvidedDocumentsCount,
    int MissingDocumentsCount,
    IReadOnlyCollection<PropertyReportChecklistItemDto> ChecklistItems);

public sealed record PropertyReportChecklistItemDto(
    string Title,
    string DocumentType,
    bool IsRequired,
    bool IsProvided,
    string Status);

public sealed record PropertyReportDocumentTypeSectionDto(
    bool IsAvailable,
    bool IsAnalyzed,
    string Status,
    string? RiskLevel,
    IReadOnlyCollection<DocumentFindingDto> KeyFindings,
    IReadOnlyCollection<string> SuggestedQuestions,
    IReadOnlyCollection<string> SuggestedNegotiationPoints);

public sealed record PropertyReportExpensesSectionDto(
    bool IsAvailable,
    bool IsAnalyzed,
    string Status,
    int? BuildingHealthScore,
    string? BuildingHealthClassification,
    ExpenseDocumentValidationDto? DocumentConfidence,
    decimal? DetectedExpenseAmount,
    string? UnitValidationStatus,
    IReadOnlyCollection<DocumentFindingDto> KeyFindings,
    IReadOnlyCollection<string> SuggestedQuestions,
    IReadOnlyCollection<string> SuggestedNegotiationPoints);

public sealed record PropertyReportFindingsSectionDto(
    IReadOnlyCollection<PropertyReportFindingGroupDto> Groups);

public sealed record PropertyReportFindingGroupDto(
    string Severity,
    string SeverityLabel,
    IReadOnlyCollection<DocumentFindingDto> Findings);
