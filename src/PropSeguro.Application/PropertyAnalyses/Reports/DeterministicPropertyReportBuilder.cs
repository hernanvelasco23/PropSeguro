using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.PropertyAnalyses.Reports;

public sealed class DeterministicPropertyReportBuilder : IPropertyReportBuilder
{
    public const string DisclaimerText =
        "Este informe no reemplaza la revision de un escribano, abogado o profesional matriculado. PropSegura te ayuda a detectar senales de alerta y preparar mejores preguntas.";

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserContext? _currentUser;

    public DeterministicPropertyReportBuilder(IApplicationDbContext dbContext, ICurrentUserContext? currentUser = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PropertyReportDto?> BuildAsync(Guid propertyAnalysisId, CancellationToken cancellationToken = default)
    {
        var analysis = await _dbContext.PropertyAnalyses
            .Include(item => item.Documents)
            .Include(item => item.RiskFindings)
            .Include(item => item.ChecklistItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propertyAnalysisId, cancellationToken);

        if (analysis is null || !_currentUser.CanAccess(analysis))
        {
            return null;
        }

        var documentFindings = await _dbContext.DocumentFindings
            .Where(item => item.PropertyAnalysisId == propertyAnalysisId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        analysis.RecalculateOpportunityScore();
        analysis.RecalculateProtectionScore(documentFindings);

        var recommendation = DetermineRecommendation(analysis, documentFindings);
        var reservationSection = BuildReservationSection(analysis, documentFindings);
        var expensesSection = BuildExpensesSection(analysis, documentFindings);
        var suggestedQuestions = Unique(documentFindings.Select(item => item.SuggestedQuestion)).ToArray();
        var suggestedNegotiationPoints = Unique(documentFindings.Select(item => item.SuggestedNegotiationPoint ?? item.SuggestedChange)).ToArray();

        return new PropertyReportDto(
            BuildPropertySummary(analysis),
            BuildExecutiveSummary(analysis, recommendation),
            recommendation.ToString(),
            RecommendationLabel(recommendation),
            RecommendationExplanation(recommendation),
            BuildOpportunitySection(analysis),
            BuildProtectionSection(analysis, documentFindings),
            BuildNegotiationSection(analysis),
            BuildDocumentsSection(analysis),
            reservationSection,
            expensesSection,
            BuildFindingsSection(documentFindings),
            suggestedQuestions,
            suggestedNegotiationPoints,
            BuildNextSteps(analysis, documentFindings, reservationSection, expensesSection),
            DisclaimerText,
            DateTime.UtcNow);
    }

    private static PropertyReportSummaryDto BuildPropertySummary(PropertyAnalysis analysis)
    {
        return new PropertyReportSummaryDto(
            analysis.Title,
            analysis.Address,
            analysis.Neighborhood,
            analysis.PriceUsd,
            analysis.TotalSquareMeters,
            analysis.UsdPerSquareMeter,
            analysis.MonthlyExpenses,
            analysis.Floor,
            analysis.ApartmentUnit,
            analysis.UnitIdentifier);
    }

    private static PropertyReportOpportunitySectionDto BuildOpportunitySection(PropertyAnalysis analysis)
    {
        return new PropertyReportOpportunitySectionDto(
            analysis.OpportunityScore,
            analysis.RecommendedUse.ToString(),
            RecommendedUseLabel(analysis.RecommendedUse),
            analysis.InitialRiskLevel.ToString(),
            RiskLevelLabel(analysis.InitialRiskLevel),
            analysis.BenchmarkComparison,
            BuildOpportunityExplanation(analysis));
    }

    private static PropertyReportProtectionSectionDto BuildProtectionSection(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings)
    {
        var missingDocuments = analysis.ChecklistItems.Count(item => item.IsRequired && !item.IsProvided);
        var highOrCritical = findings.Count(item => item.Severity is FindingSeverity.High or FindingSeverity.Critical);
        var confidenceSummary = BuildDocumentConfidenceSummary(analysis);

        return new PropertyReportProtectionSectionDto(
            analysis.ProtectionScore,
            confidenceSummary,
            missingDocuments,
            highOrCritical,
            highOrCritical > 0
                ? "Hay hallazgos relevantes que conviene resolver antes de comprometer dinero."
                : missingDocuments > 0
                    ? "La proteccion documental todavia esta incompleta porque faltan documentos clave."
                    : "La documentacion cargada no muestra alertas graves con las reglas actuales.");
    }

    private static PropertyReportNegotiationSectionDto BuildNegotiationSection(PropertyAnalysis analysis)
    {
        return new PropertyReportNegotiationSectionDto(
            analysis.PriceUsd,
            analysis.EstimatedFairValueMinUsd,
            analysis.EstimatedFairValueMaxUsd,
            analysis.SuggestedFirstOfferUsd,
            analysis.SuggestedTargetPriceUsd,
            analysis.NegotiationMarginPercent,
            analysis.MarketPosition.ToString(),
            MarketPositionLabel(analysis.MarketPosition),
            analysis.NegotiationExplanation);
    }

    private static PropertyReportDocumentsSectionDto BuildDocumentsSection(PropertyAnalysis analysis)
    {
        var required = analysis.ChecklistItems.Count(item => item.IsRequired);
        var provided = analysis.ChecklistItems.Count(item => item.IsRequired && item.IsProvided);

        return new PropertyReportDocumentsSectionDto(
            required,
            provided,
            required - provided,
            analysis.ChecklistItems
                .OrderBy(item => item.DocumentType)
                .Select(item => new PropertyReportChecklistItemDto(
                    item.Title,
                    item.DocumentType.ToString(),
                    item.IsRequired,
                    item.IsProvided,
                    item.Status.ToString()))
                .ToArray());
    }

    private static PropertyReportDocumentTypeSectionDto BuildReservationSection(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings)
    {
        var documents = analysis.Documents
            .Where(item => item.DocumentType == DocumentType.ReservationAgreement)
            .OrderByDescending(item => item.AnalyzedAtUtc ?? item.UploadedAtUtc)
            .ToArray();
        var document = documents.FirstOrDefault();

        if (document is null)
        {
            return EmptyDocumentTypeSection("Reserva no analizada todavia.");
        }

        var documentFindings = findings
            .Where(item => item.PropertyDocumentId == document.Id)
            .OrderByDescending(item => item.Severity)
            .ToArray();

        return new PropertyReportDocumentTypeSectionDto(
            true,
            document.AnalysisStatus == DocumentAnalysisStatus.Completed,
            document.AnalysisStatus == DocumentAnalysisStatus.Completed ? "Reserva analizada." : "Reserva cargada, pendiente de analisis.",
            RiskLevel(documentFindings),
            documentFindings.Take(6).Select(item => item.ToDto()).ToArray(),
            Unique(documentFindings.Select(item => item.SuggestedQuestion)).ToArray(),
            Unique(documentFindings.Select(item => item.SuggestedNegotiationPoint ?? item.SuggestedChange)).ToArray());
    }

    private static PropertyReportExpensesSectionDto BuildExpensesSection(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings)
    {
        var documents = analysis.Documents
            .Where(item => item.DocumentType == DocumentType.Expenses)
            .OrderByDescending(item => item.AnalyzedAtUtc ?? item.UploadedAtUtc)
            .ToArray();
        var document = documents.FirstOrDefault();

        if (document is null)
        {
            return EmptyExpensesSection("Expensas no analizadas todavia.");
        }

        var documentFindings = findings
            .Where(item => item.PropertyDocumentId == document.Id)
            .OrderByDescending(item => item.Severity)
            .ToArray();
        var expenseValidation = ExpenseDocumentValidator.Validate(analysis, document, document.EditedText ?? document.ExtractedText);
        var expenseIntelligence = analysis.ToExpenseIntelligenceAnalysisDto(findings);

        return new PropertyReportExpensesSectionDto(
            true,
            document.AnalysisStatus == DocumentAnalysisStatus.Completed,
            document.AnalysisStatus == DocumentAnalysisStatus.Completed ? "Expensas analizadas." : "Expensas cargadas, pendientes de analisis.",
            expenseIntelligence?.BuildingHealthScore,
            expenseIntelligence?.Classification,
            expenseValidation.ToDto(),
            expenseValidation.Extraction.DetectedExpenseAmount,
            expenseValidation.Label,
            documentFindings.Take(6).Select(item => item.ToDto()).ToArray(),
            Unique(documentFindings.Select(item => item.SuggestedQuestion)).ToArray(),
            Unique(documentFindings.Select(item => item.SuggestedNegotiationPoint ?? item.SuggestedChange)).ToArray());
    }

    private static PropertyReportFindingsSectionDto BuildFindingsSection(IReadOnlyCollection<DocumentFinding> findings)
    {
        var groups = new[]
        {
            FindingSeverity.Critical,
            FindingSeverity.High,
            FindingSeverity.Medium,
            FindingSeverity.Low,
            FindingSeverity.Info
        }
            .Select(severity => new PropertyReportFindingGroupDto(
                severity.ToString(),
                FindingSeverityLabel(severity),
                findings
                    .Where(item => item.Severity == severity)
                    .OrderBy(item => item.CreatedAtUtc)
                    .Select(item => item.ToDto())
                    .ToArray()))
            .Where(group => group.Findings.Count > 0)
            .ToArray();

        return new PropertyReportFindingsSectionDto(groups);
    }

    private static OverallRecommendation DetermineRecommendation(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings)
    {
        if (!analysis.PriceUsd.HasValue || !analysis.TotalSquareMeters.HasValue || string.IsNullOrWhiteSpace(analysis.Neighborhood))
        {
            return OverallRecommendation.InsufficientData;
        }

        if (findings.Any(item => item.Severity == FindingSeverity.Critical) ||
            analysis.ProtectionScore < 30 ||
            findings.Any(item => item.Title == "Las expensas podrian corresponder a otra unidad"))
        {
            return OverallRecommendation.Avoid;
        }

        if (analysis.MarketPosition == MarketPosition.Overpriced ||
            findings.Any(item => item.Title == "Las expensas del documento no coinciden con la publicacion") ||
            findings.Any(item => item.Category == FindingCategory.Financial && item.Severity == FindingSeverity.High))
        {
            return OverallRecommendation.Renegotiate;
        }

        var missingKeyDocuments = analysis.ChecklistItems.Any(item => item.IsRequired && !item.IsProvided);
        var hasReservationAnalyzed = analysis.Documents.Any(item => item.DocumentType == DocumentType.ReservationAgreement && item.AnalysisStatus == DocumentAnalysisStatus.Completed);
        var hasExpensesAnalyzed = analysis.Documents.Any(item => item.DocumentType == DocumentType.Expenses && item.AnalysisStatus == DocumentAnalysisStatus.Completed);

        if (missingKeyDocuments || analysis.ProtectionScore < 50 || !hasReservationAnalyzed || !hasExpensesAnalyzed)
        {
            return OverallRecommendation.WaitForDocuments;
        }

        if (analysis.OpportunityScore >= 75 &&
            analysis.ProtectionScore >= 70 &&
            !findings.Any(item => item.Severity is FindingSeverity.High or FindingSeverity.Critical))
        {
            return OverallRecommendation.Advance;
        }

        if (analysis.OpportunityScore >= 65)
        {
            return OverallRecommendation.AdvanceWithCaution;
        }

        return OverallRecommendation.WaitForDocuments;
    }

    private static string BuildExecutiveSummary(PropertyAnalysis analysis, OverallRecommendation recommendation)
    {
        var opportunity = analysis.OpportunityScore >= 75
            ? "muestra una oportunidad interesante"
            : analysis.OpportunityScore >= 50
                ? "tiene potencial, pero requiere mas informacion"
                : "todavia no muestra una oportunidad clara";
        var protection = analysis.ProtectionScore >= 70
            ? "la proteccion documental es razonable"
            : "todavia requiere validacion documental";

        return $"La propiedad {opportunity} segun los datos iniciales y {protection} antes de avanzar. Recomendacion actual: {RecommendationLabel(recommendation).ToLowerInvariant()}.";
    }

    private static string BuildOpportunityExplanation(PropertyAnalysis analysis)
    {
        if (analysis.OpportunityScore >= 75)
        {
            return "Los datos iniciales son favorables para seguir evaluando la propiedad.";
        }

        if (analysis.OpportunityScore >= 50)
        {
            return "La propiedad puede ser evaluable, pero conviene completar datos y revisar precio, expensas y estado.";
        }

        return "Faltan datos o hay senales iniciales que reducen la conveniencia de avanzar.";
    }

    private static string BuildDocumentConfidenceSummary(PropertyAnalysis analysis)
    {
        var expenseDocuments = analysis.Documents.Where(item => item.DocumentType == DocumentType.Expenses).ToArray();

        if (expenseDocuments.Length == 0)
        {
            return "No hay expensas cargadas para validar unidad e importe.";
        }

        var latest = expenseDocuments.OrderByDescending(item => item.AnalyzedAtUtc ?? item.UploadedAtUtc).First();
        var validation = ExpenseDocumentValidator.Validate(analysis, latest, latest.EditedText ?? latest.ExtractedText);

        return $"{validation.Label} ({validation.ConfidenceScore}%). {validation.Explanation}";
    }

    private static IReadOnlyCollection<string> BuildNextSteps(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportDocumentTypeSectionDto reservationSection,
        PropertyReportExpensesSectionDto expensesSection)
    {
        var steps = new List<string>();

        if (analysis.ChecklistItems.Any(item => item.DocumentType == DocumentType.DomainReport && !item.IsProvided))
        {
            steps.Add("Solicitar informe de dominio.");
        }

        if (expensesSection.DocumentConfidence?.Classification is "Mismatch" or "NotValidated" or "PartiallyValidated")
        {
            steps.Add("Confirmar que las expensas corresponden a la unidad publicada.");
        }

        if (findings.Any(item => item.Title == "Reserva no reintegrable"))
        {
            steps.Add("Pedir aclaracion sobre clausula de reserva no reintegrable.");
        }

        if (findings.Any(item => item.Title == "Expensas extraordinarias detectadas"))
        {
            steps.Add("Validar si existen expensas extraordinarias pendientes.");
        }

        if (!reservationSection.IsAnalyzed)
        {
            steps.Add("Analizar la reserva antes de firmar o transferir dinero.");
        }

        if (!expensesSection.IsAnalyzed)
        {
            steps.Add("Analizar las expensas para validar costos mensuales y riesgos del consorcio.");
        }

        steps.Add("Consultar con escribano antes de firmar.");

        return steps.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string RiskLevel(IReadOnlyCollection<DocumentFinding> findings)
    {
        if (findings.Any(item => item.Severity == FindingSeverity.Critical))
        {
            return "Critical";
        }

        if (findings.Any(item => item.Severity == FindingSeverity.High))
        {
            return "High";
        }

        if (findings.Any(item => item.Severity == FindingSeverity.Medium))
        {
            return "Medium";
        }

        if (findings.Any(item => item.Severity == FindingSeverity.Low))
        {
            return "Low";
        }

        return "Info";
    }

    private static PropertyReportDocumentTypeSectionDto EmptyDocumentTypeSection(string status)
    {
        return new PropertyReportDocumentTypeSectionDto(false, false, status, null, [], [], []);
    }

    private static PropertyReportExpensesSectionDto EmptyExpensesSection(string status)
    {
        return new PropertyReportExpensesSectionDto(false, false, status, null, null, null, null, null, [], [], []);
    }

    private static IEnumerable<string> Unique(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string RecommendationLabel(OverallRecommendation recommendation)
    {
        return recommendation switch
        {
            OverallRecommendation.Advance => "Seguiria avanzando",
            OverallRecommendation.AdvanceWithCaution => "Avanzaria con cautela",
            OverallRecommendation.WaitForDocuments => "Esperaria documentacion",
            OverallRecommendation.Renegotiate => "Renegociaria",
            OverallRecommendation.Avoid => "No avanzaria",
            _ => "Faltan datos"
        };
    }

    private static string RecommendationExplanation(OverallRecommendation recommendation)
    {
        return recommendation switch
        {
            OverallRecommendation.Advance => "La oportunidad es buena y la proteccion documental es aceptable con las reglas actuales.",
            OverallRecommendation.AdvanceWithCaution => "La oportunidad parece buena, pero todavia hay riesgos o validaciones pendientes.",
            OverallRecommendation.WaitForDocuments => "Conviene completar documentacion clave antes de avanzar con una reserva.",
            OverallRecommendation.Renegotiate => "Hay senales de precio, expensas o riesgos financieros que justifican renegociar.",
            OverallRecommendation.Avoid => "Hay riesgos severos o incompatibilidades que conviene resolver antes de seguir.",
            _ => "Faltan datos basicos para emitir una recomendacion confiable."
        };
    }

    private static string RecommendedUseLabel(RecommendedUse recommendedUse)
    {
        return recommendedUse switch
        {
            RecommendedUse.Living => "Vivienda",
            RecommendedUse.Investment => "Inversion",
            RecommendedUse.Both => "Vivienda e inversion",
            RecommendedUse.NotRecommended => "No recomendado",
            _ => "Sin recomendacion"
        };
    }

    private static string RiskLevelLabel(InitialRiskLevel riskLevel)
    {
        return riskLevel switch
        {
            InitialRiskLevel.Low => "Bajo",
            InitialRiskLevel.Medium => "Medio",
            _ => "Alto"
        };
    }

    private static string MarketPositionLabel(MarketPosition marketPosition)
    {
        return marketPosition switch
        {
            MarketPosition.Cheap => "Barato",
            MarketPosition.Fair => "En precio",
            MarketPosition.Expensive => "Caro",
            MarketPosition.Overpriced => "Sobreprecio",
            _ => "Sin datos"
        };
    }

    private static string FindingSeverityLabel(FindingSeverity severity)
    {
        return severity switch
        {
            FindingSeverity.Critical => "Critico",
            FindingSeverity.High => "Alto",
            FindingSeverity.Medium => "Medio",
            FindingSeverity.Low => "Bajo",
            _ => "Info"
        };
    }

    private enum OverallRecommendation
    {
        Advance = 1,
        AdvanceWithCaution = 2,
        WaitForDocuments = 3,
        Renegotiate = 4,
        Avoid = 5,
        InsufficientData = 6
    }
}
