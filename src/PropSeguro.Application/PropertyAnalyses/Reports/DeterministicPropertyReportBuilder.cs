using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Application.PropertyAnalyses;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.PropertyAnalyses.Reports;

public sealed class DeterministicPropertyReportBuilder : IPropertyReportBuilder
{
    public const string DisclaimerText =
        "Este informe no reemplaza la revisión de un escribano, abogado o profesional matriculado. PropSegura te ayuda a ordenar información, detectar señales de alerta y preparar mejores preguntas antes de avanzar.";

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
        var reportFindings = DistinctFindingsByStableCode(documentFindings);

        analysis.RecalculateOpportunityScore();
        analysis.RecalculateProtectionScore(reportFindings);

        var recommendation = DetermineRecommendation(analysis, reportFindings);
        var reservationSection = BuildReservationSection(analysis, reportFindings);
        var expensesSection = BuildExpensesSection(analysis, reportFindings);
        var reportStatus = DetermineReportStatus(analysis, reportFindings);
        var generatedAtUtc = DateTime.UtcNow;
        var suggestedQuestions = Unique(reportFindings.Select(item => item.SuggestedQuestion)).ToArray();
        var suggestedNegotiationPoints = Unique(reportFindings.Select(item => item.SuggestedNegotiationPoint ?? item.SuggestedChange)).ToArray();
        var negotiationOpportunities = BuildNegotiationOpportunities(analysis, reportFindings, expensesSection);

        return new PropertyReportDto(
            BuildPropertySummary(analysis),
            reportStatus.ToString(),
            ReportStatusLabel(reportStatus),
            BuildCoverSection(analysis, reportStatus, generatedAtUtc),
            BuildExecutiveSummary(analysis, recommendation),
            BuildExecutiveConclusion(analysis, recommendation, reportFindings),
            recommendation.ToString(),
            RecommendationLabel(recommendation),
            RecommendationExplanation(recommendation),
            BuildIdentifiedRisks(analysis, reportFindings, expensesSection),
            BuildDetectedStrengths(analysis, reportFindings, reservationSection, expensesSection),
            BuildZoneStrengths(analysis),
            BuildNegotiationLevers(negotiationOpportunities),
            BuildOpportunitySection(analysis),
            BuildProtectionSection(analysis, reportFindings),
            BuildNegotiationSection(analysis, reportFindings, expensesSection),
            BuildDocumentsSection(analysis),
            reservationSection,
            expensesSection,
            BuildFindingsSection(reportFindings),
            BuildWhatThisMeansForYou(analysis, reportFindings, reservationSection, expensesSection),
            BuildConditionsToAdvance(analysis, reportFindings, reservationSection, expensesSection),
            BuildMoneyRisks(analysis, reportFindings),
            negotiationOpportunities,
            BuildNegotiationArguments(analysis, reportFindings, expensesSection),
            BuildQuestionsForSellerOrAgent(analysis, reportFindings, expensesSection),
            BuildQuestionsForNotaryOrLawyer(reportFindings, reservationSection),
            BuildReviewedDocuments(analysis),
            BuildPendingDocuments(analysis),
            suggestedQuestions,
            suggestedNegotiationPoints,
            BuildNextSteps(analysis, reportFindings, reservationSection, expensesSection),
            DisclaimerText,
            generatedAtUtc);
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

    private static PropertyReportCoverSectionDto BuildCoverSection(
        PropertyAnalysis analysis,
        ReportStatus reportStatus,
        DateTime generatedAtUtc)
    {
        return new PropertyReportCoverSectionDto(
            "Informe PropSegura",
            "Segunda opinión para tomar una mejor decisión antes de reservar o comprar.",
            analysis.Title,
            analysis.Address,
            analysis.Neighborhood,
            analysis.PriceUsd,
            analysis.TotalSquareMeters,
            analysis.UsdPerSquareMeter,
            analysis.MonthlyExpenses,
            analysis.GetCoverImageUrl(),
            analysis.CoverImageStoragePath,
            reportStatus.ToString(),
            ReportStatusLabel(reportStatus),
            generatedAtUtc);
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

    private static PropertyReportNegotiationSectionDto BuildNegotiationSection(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportExpensesSectionDto expensesSection)
    {
        return new PropertyReportNegotiationSectionDto(
            analysis.PriceUsd,
            analysis.EstimatedFairValueMinUsd,
            analysis.EstimatedFairValueMaxUsd,
            analysis.SuggestedFirstOfferUsd,
            analysis.SuggestedTargetPriceUsd,
            analysis.MaxRecommendedPriceUsd,
            analysis.NegotiationMarginPercent,
            analysis.MarketPosition.ToString(),
            MarketPositionLabel(analysis.MarketPosition),
            analysis.NegotiationExplanation,
            BuildSuggestedNegotiationStrategy(analysis, findings, expensesSection));
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

        var documentFindings = DistinctFindingsByStableCode(findings
            .Where(item => item.PropertyDocumentId == document.Id)
            .OrderByDescending(item => item.Severity))
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
        var expenseBenchmark = BuildExpenseBenchmark(analysis);
        var documents = analysis.Documents
            .Where(item => item.DocumentType == DocumentType.Expenses)
            .OrderByDescending(item => item.AnalyzedAtUtc ?? item.UploadedAtUtc)
            .ToArray();
        var document = documents.FirstOrDefault();

        if (document is null)
        {
            return EmptyExpensesSection("Expensas no analizadas todavia.", analysis, expenseBenchmark);
        }

        var expenseValidation = ExpenseDocumentValidator.Validate(analysis, document, document.EditedText ?? document.ExtractedText);
        var documentFindings = DistinctFindingsByStableCode(findings
            .Where(item => item.PropertyDocumentId == document.Id)
            .Concat(expenseValidation.Findings)
            .OrderByDescending(item => item.Severity))
            .ToArray();
        var expenseIntelligence = analysis.ToExpenseIntelligenceAnalysisDto(findings);

        return new PropertyReportExpensesSectionDto(
            true,
            document.AnalysisStatus == DocumentAnalysisStatus.Completed,
            document.AnalysisStatus == DocumentAnalysisStatus.Completed ? "Expensas analizadas." : "Expensas cargadas, pendientes de analisis.",
            "Documento de expensas cargado",
            expenseIntelligence?.BuildingHealthScore,
            expenseIntelligence?.Classification,
            expenseValidation.ToDto(),
            expenseValidation.Extraction.DetectedExpenseAmount,
            analysis.MonthlyExpenses,
            analysis.TotalSquareMeters,
            expenseBenchmark.MonthlyExpensesPerSquareMeter,
            expenseBenchmark.BenchmarkMonthlyExpensesPerSquareMeter,
            expenseBenchmark.Comparison,
            BuildPublishedVsDocumentStatus(analysis.MonthlyExpenses, expenseValidation.Extraction.DetectedExpenseAmount),
            expenseValidation.Label,
            document.UserConfirmedMatchesProperty,
            document.UserConfirmedMatchesPropertyAtUtc,
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
            analysis.ProtectionScore < 30)
        {
            return OverallRecommendation.ProfessionalReview;
        }

        if (analysis.MarketPosition == MarketPosition.Overpriced ||
            findings.Any(item => IsExpenseValueMismatch(item)) ||
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

    private static ReportStatus DetermineReportStatus(PropertyAnalysis analysis, IReadOnlyCollection<DocumentFinding> findings)
    {
        var hasCoreData = analysis.PriceUsd.HasValue &&
            analysis.TotalSquareMeters.HasValue &&
            !string.IsNullOrWhiteSpace(analysis.Neighborhood);
        var hasAnyKeyDocumentAnalyzed = analysis.Documents.Any(item =>
            item.DocumentType is DocumentType.Expenses or DocumentType.ReservationAgreement &&
            item.AnalysisStatus == DocumentAnalysisStatus.Completed);
        var hasBlockingInconsistency = findings.Any(item =>
            item.Severity == FindingSeverity.Critical ||
            item.Severity == FindingSeverity.High ||
            IsExpenseValueMismatch(item)) ||
            analysis.Documents.Any(item =>
                item.DocumentType == DocumentType.ReservationAgreement &&
                item.AnalysisStatus == DocumentAnalysisStatus.Completed &&
                findings.Any(finding => finding.PropertyDocumentId == item.Id && finding.Severity is FindingSeverity.High or FindingSeverity.Critical));

        if (!hasCoreData || !hasAnyKeyDocumentAnalyzed)
        {
            return ReportStatus.Incomplete;
        }

        return hasBlockingInconsistency ? ReportStatus.NeedsReview : ReportStatus.Ready;
    }

    private static string BuildExecutiveSummary(PropertyAnalysis analysis, OverallRecommendation recommendation)
    {
        var opportunity = analysis.OpportunityScore >= 75
            ? "muestra una oportunidad interesante"
            : analysis.OpportunityScore >= 50
                ? "tiene potencial, pero requiere más información"
                : "todavía no muestra una oportunidad clara";
        var protection = analysis.ProtectionScore >= 70
            ? "la protección documental es razonable"
            : "todavía requiere validación documental";

        return $"La propiedad {opportunity} según los datos iniciales y {protection} antes de avanzar. Recomendación actual: {RecommendationLabel(recommendation).ToLowerInvariant()}.";
    }

    private static string BuildExecutiveConclusion(
        PropertyAnalysis analysis,
        OverallRecommendation recommendation,
        IReadOnlyCollection<DocumentFinding> findings)
    {
        return recommendation switch
        {
            OverallRecommendation.Advance =>
                "Avanzar. La propiedad muestra una oportunidad razonable y la documentación analizada no presenta alertas graves con las reglas actuales.",
            OverallRecommendation.AdvanceWithCaution =>
                "Avanzar negociando. La propiedad puede ser interesante, pero todavía conviene resolver algunos puntos documentales antes de comprometer dinero.",
            OverallRecommendation.WaitForDocuments =>
                "Resolver pendientes antes de avanzar. Antes de reservar, conviene completar los documentos clave y validar que no existan riesgos que cambien la decisión.",
            OverallRecommendation.Renegotiate =>
                "Avanzar negociando. Hay señales de precio, expensas o riesgos financieros que pueden usarse para pedir mejores condiciones.",
            OverallRecommendation.ProfessionalReview =>
                "Hay hallazgos relevantes. Vale la pena revisarlos con un profesional y pedir aclaraciones concretas antes de comprometer dinero.",
            _ when findings.Any(item => item.Severity is FindingSeverity.High or FindingSeverity.Critical) =>
                "Requiere revisión. Hay alertas relevantes que conviene conversar con la inmobiliaria y revisar con un profesional antes de seguir.",
            _ when analysis.OpportunityScore >= 65 =>
                "La propiedad podría valer la pena, pero el informe todavía necesita más datos para sostener una recomendación sólida.",
            _ =>
                "Faltan datos importantes para emitir una conclusión confiable. Completá precio, metros, barrio y al menos un documento clave."
        };
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

    private static IReadOnlyCollection<string> BuildIdentifiedRisks(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportExpensesSectionDto expensesSection)
    {
        var risks = new List<string>();

        risks.AddRange(findings
            .Where(item => item.Severity is FindingSeverity.Critical or FindingSeverity.High or FindingSeverity.Medium)
            .Select(BuildRiskSummary));

        if (analysis.GetVisitObservations().Any(IsStrongVisitObservation))
        {
            risks.Add("Observaciones relevantes de visita: pueden anticipar reparaciones, costos o necesidad de revision profesional antes de ofertar.");
        }

        if (expensesSection.ExpenseBenchmarkComparison == "Muy por encima del benchmark de expensas")
        {
            risks.Add("Expensas por m2 muy por encima del benchmark: pueden aumentar el costo mensual real frente a alternativas comparables.");
        }

        return risks
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyCollection<string> BuildDetectedStrengths(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportDocumentTypeSectionDto reservationSection,
        PropertyReportExpensesSectionDto expensesSection)
    {
        var strengths = new List<string>();

        if (analysis.MarketPosition is MarketPosition.Cheap or MarketPosition.Fair)
        {
            strengths.Add("Precio competitivo o alineado al benchmark de la zona.");
        }

        if (expensesSection.ExpenseBenchmarkComparison is "Por debajo del benchmark de expensas" or "En linea con el benchmark de expensas")
        {
            strengths.Add("Expensas dentro de parametros razonables para la zona.");
        }

        if (expensesSection.BuildingHealthScore >= 70)
        {
            strengths.Add($"Salud del edificio {expensesSection.BuildingHealthScore} - {expensesSection.BuildingHealthClassification ?? "Buena"}.");
        }

        if (expensesSection.IsAnalyzed && !HasExpenseOperatingSignals(expensesSection.KeyFindings))
        {
            strengths.Add("Documento de expensas sin senales de deuda, morosidad u obras relevantes.");
        }

        if (reservationSection.IsAnalyzed && !findings.Any(item => item.Category == FindingCategory.Legal && item.Severity == FindingSeverity.Critical))
        {
            strengths.Add("Sin hallazgos legales criticos detectados en la documentacion analizada.");
        }

        if (reservationSection.IsAnalyzed && reservationSection.KeyFindings.Count == 0)
        {
            strengths.Add("Reserva sin clausulas relevantes detectadas por las reglas actuales.");
        }

        return strengths
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }

    private static IReadOnlyCollection<string> BuildZoneStrengths(PropertyAnalysis analysis)
    {
        var benchmark = NeighborhoodBenchmark.Find(analysis.Neighborhood);
        if (benchmark is null)
        {
            return [];
        }

        var strengths = new List<string>();
        var neighborhood = benchmark.Neighborhood;

        if (benchmark.InvestmentDemandLevel == DemandLevel.High || benchmark.LivingDemandLevel == DemandLevel.High)
        {
            strengths.Add($"{neighborhood} presenta demanda inmobiliaria alta para compra residencial o inversion.");
        }

        strengths.Add($"{neighborhood} es una zona consolidada, con oferta comparable y servicios urbanos establecidos.");

        if (benchmark.InvestmentDemandLevel == DemandLevel.High)
        {
            strengths.Add("La demanda de alquiler favorece analizar la propiedad como activo de renta.");
        }

        if (benchmark.LivingDemandLevel == DemandLevel.High)
        {
            strengths.Add("La demanda para vivienda propia puede favorecer liquidez futura y reventa.");
        }

        return strengths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }

    private static IReadOnlyCollection<string> BuildNegotiationLevers(
        IReadOnlyCollection<PropertyReportNegotiationOpportunityDto> opportunities)
    {
        return opportunities
            .Select(item => $"{item.Title}: {item.SuggestedAction}")
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static IReadOnlyCollection<string> BuildWhatThisMeansForYou(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportDocumentTypeSectionDto reservationSection,
        PropertyReportExpensesSectionDto expensesSection)
    {
        var items = new List<string>();

        if (expensesSection.DocumentConfidence?.Classification is ExpenseDocumentValidator.ValueMismatchClassification)
        {
            items.Add("Podrías estar evaluando la propiedad con un valor de expensas incorrecto.");
        }

        if (expensesSection.DocumentConfidence?.Classification is "PartiallyValidated" or "NotValidated")
        {
            items.Add("Todavía no está completamente validado que las expensas correspondan a la unidad publicada.");
        }

        if (findings.Any(item => item.Title == "Reserva no reintegrable"))
        {
            items.Add("La reserva debería condicionarse a revisión documental satisfactoria y reglas claras de devolución.");
        }

        if (analysis.MarketPosition is MarketPosition.Fair or MarketPosition.Cheap)
        {
            items.Add("El precio parece dentro o por debajo del rango esperado para la zona según el benchmark disponible.");
        }
        else if (analysis.MarketPosition is MarketPosition.Expensive or MarketPosition.Overpriced)
        {
            items.Add("El precio publicado parece por encima del benchmark y puede requerir una negociación más firme.");
        }

        if (expensesSection.ExpenseBenchmarkComparison == "Por debajo del benchmark de expensas")
        {
            items.Add("Las expensas publicadas estan por debajo del benchmark mensual por m2 de la zona; no aparecen como una alerta de costo con esta regla.");
        }

        if (findings.Any(item => item.Title.Contains("extraordinarias", StringComparison.OrdinalIgnoreCase)))
        {
            items.Add("Antes de avanzar conviene confirmar si existen expensas extraordinarias pendientes.");
        }

        if (!reservationSection.IsAnalyzed)
        {
            items.Add("Antes de firmar o transferir dinero conviene analizar la reserva.");
        }

        if (!expensesSection.IsAnalyzed)
        {
            items.Add("Antes de reservar conviene revisar expensas para detectar costos ocultos o señales del consorcio.");
        }

        return WithFallback(items, "El informe todavía necesita más datos para traducir los riesgos en consecuencias concretas para vos.");
    }

    private static IReadOnlyCollection<string> BuildConditionsToAdvance(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportDocumentTypeSectionDto reservationSection,
        PropertyReportExpensesSectionDto expensesSection)
    {
        var conditions = new List<string>();

        if (RequiresExpenseUnitConfirmation(expensesSection.DocumentConfidence?.Classification))
        {
            conditions.Add("Confirmar que las expensas corresponden a la unidad publicada.");
        }

        if (findings.Any(item => item.Title.Contains("extraordinarias", StringComparison.OrdinalIgnoreCase)))
        {
            conditions.Add("Verificar si hay expensas extraordinarias pendientes.");
        }

        if (findings.Any(item => item.Title.Contains("no reintegrable", StringComparison.OrdinalIgnoreCase) ||
            item.Title.Contains("devolucion", StringComparison.OrdinalIgnoreCase) ||
            item.Title.Contains("penalidad", StringComparison.OrdinalIgnoreCase)))
        {
            conditions.Add("Pedir una reserva con devolución ante rechazo crediticio o problemas documentales.");
        }

        if (analysis.ChecklistItems.Any(item => item.DocumentType == DocumentType.DomainReport && !item.IsProvided))
        {
            conditions.Add("Solicitar informe de dominio e inhibiciones.");
        }

        if (!reservationSection.IsAnalyzed)
        {
            conditions.Add("Analizar la reserva antes de firmar o transferir dinero.");
        }

        if (!expensesSection.IsAnalyzed)
        {
            conditions.Add("Analizar las expensas para validar costos mensuales y riesgos del consorcio.");
        }

        conditions.Add("Consultar con escribano o abogado antes de firmar.");

        return conditions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyCollection<PropertyReportMoneyRiskDto> BuildMoneyRisks(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings)
    {
        var risks = new List<PropertyReportMoneyRiskDto>();

        foreach (var finding in findings.Where(IsMoneyRiskFinding))
        {
            risks.Add(new PropertyReportMoneyRiskDto(
                finding.Title,
                MoneyRiskWhyItMatters(finding),
                finding.BuyerImpact ?? finding.Description,
                finding.SuggestedNegotiationPoint ?? finding.SuggestedChange ?? "Pedir aclaración por escrito antes de avanzar.",
                SourceDocumentType(analysis, finding),
                SourceDocumentLabel(analysis, finding)));
        }

        if (analysis.MarketPosition is MarketPosition.Expensive or MarketPosition.Overpriced)
        {
            risks.Add(new PropertyReportMoneyRiskDto(
                "Precio por encima del benchmark",
                "Pagar por encima del promedio estimado reduce margen de negociación y puede afectar la reventa.",
                "Podrías comprometer más capital del razonable para una propiedad comparable.",
                "Usar el precio por m² y el benchmark como argumento para ajustar la oferta.",
                null,
                null));
        }

        var expenseBenchmark = BuildExpenseBenchmark(analysis);
        if (expenseBenchmark.Ratio > 1.35m)
        {
            risks.Add(new PropertyReportMoneyRiskDto(
                "Expensas altas frente al benchmark de la zona",
                "Las expensas por m2 superan ampliamente el benchmark mensual de la zona.",
                "La propiedad puede ser mas cara de sostener que alternativas comparables del mismo barrio.",
                "Pedir detalle de expensas ordinarias, extraordinarias y deudas del consorcio antes de ofertar.",
                "Expenses",
                "Expensas"));
        }

        return risks
            .GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyCollection<string> BuildNegotiationArguments(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportExpensesSectionDto expensesSection)
    {
        var arguments = new List<string>();

        if (analysis.MarketPosition is MarketPosition.Expensive or MarketPosition.Overpriced)
        {
            arguments.Add("Precio por m2 por encima del valor justo estimado.");
        }

        if (analysis.GetVisitObservations().Any(IsStrongVisitObservation))
        {
            arguments.Add("Observaciones de visita que conviene validar antes de mejorar la oferta.");
        }

        if (RequiresExpenseUnitConfirmation(expensesSection.DocumentConfidence?.Classification) ||
            expensesSection.DocumentConfidence?.Classification == ExpenseDocumentValidator.ValueMismatchClassification)
        {
            arguments.Add("Validaciones pendientes sobre expensas o documentacion.");
        }

        if (analysis.ChecklistItems.Any(item => item.IsRequired && !item.IsProvided))
        {
            arguments.Add("Documentacion pendiente para cerrar la validacion.");
        }

        return WithFallback(arguments, "Todavía no hay argumentos fuertes de negociación detectados con la información cargada.");
    }

    private static IReadOnlyCollection<PropertyReportNegotiationOpportunityDto> BuildNegotiationOpportunities(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportExpensesSectionDto expensesSection)
    {
        var opportunities = new List<PropertyReportNegotiationOpportunityDto>();
        var observations = analysis.GetVisitObservations().ToArray();
        var hasHumidityObservation = observations.Any(IsHumidityObservation);
        var hasReservationHumidityFinding = findings.Any(finding =>
            IsReservationFinding(analysis, finding) &&
            MentionsHumidity(finding));

        if (hasHumidityObservation && hasReservationHumidityFinding)
        {
            opportunities.Add(new PropertyReportNegotiationOpportunityDto(
                "Humedad observada + mencionada en la reserva",
                "El comprador observo humedad durante la visita y la reserva tambien contiene una clausula relacionada. Esto refuerza la necesidad de pedir aclaraciones, documentacion adicional o una mejora en las condiciones de la operacion.",
                "Alto",
                "Pedir aclaracion por escrito, solicitar revision profesional y usarlo como argumento de renegociacion."));
        }

        foreach (var observation in observations)
        {
            var opportunity = BuildVisitObservationOpportunity(observation);
            if (opportunity is not null &&
                !(IsHumidityObservation(observation) && hasReservationHumidityFinding))
            {
                opportunities.Add(opportunity);
            }
        }

        if (analysis.MarketPosition is MarketPosition.Expensive or MarketPosition.Overpriced)
        {
            opportunities.Add(new PropertyReportNegotiationOpportunityDto(
                "Precio publicado por encima del valor justo estimado",
                "El precio publicado queda por encima del rango estimado para la zona y los metros cargados. Conviene separar el valor emocional del inmueble del valor comparable de mercado.",
                analysis.MarketPosition == MarketPosition.Overpriced ? "Alto" : "Medio",
                "Usar el rango de valor justo y el precio por m2 como base para ajustar la oferta."));
        }

        if (expensesSection.ExpenseBenchmarkComparison == "Muy por encima del benchmark de expensas")
        {
            opportunities.Add(new PropertyReportNegotiationOpportunityDto(
                "Expensas altas frente al benchmark de la zona",
                "Las expensas por m2 cargadas superan ampliamente el benchmark mensual de la zona. Esto puede afectar el costo real de sostener la propiedad.",
                "Medio",
                "Pedir liquidacion detallada, extraordinarias y deudas del consorcio antes de ofertar."));
        }

        foreach (var finding in findings.Where(IsNegotiationUsefulFinding))
        {
            opportunities.Add(new PropertyReportNegotiationOpportunityDto(
                finding.Title,
                finding.BuyerImpact ?? finding.Description,
                ImpactLabel(finding.Severity),
                finding.SuggestedNegotiationPoint ?? finding.SuggestedChange ?? "Pedir aclaracion por escrito antes de avanzar."));
        }

        return opportunities
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static string BuildSuggestedNegotiationStrategy(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportExpensesSectionDto expensesSection)
    {
        var observations = analysis.GetVisitObservations().ToArray();
        var hasStrongObservation = observations.Any(IsStrongVisitObservation);
        var hasMinorObservation = observations.Length > 0 && !hasStrongObservation;
        var hasHighPrice = analysis.MarketPosition is MarketPosition.Expensive or MarketPosition.Overpriced;
        var hasCriticalDocumentRisk = findings.Any(item => item.Severity == FindingSeverity.Critical);
        var hasRelevantDocumentSignal = findings.Any(IsNegotiationUsefulFinding);
        var hasHighExpenses = expensesSection.ExpenseBenchmarkComparison == "Muy por encima del benchmark de expensas";

        if (hasCriticalDocumentRisk)
        {
            return "Antes de firmar conviene aclarar los riesgos documentales criticos y pedir respaldo por escrito. La negociacion deberia priorizar condiciones de salida, documentacion pendiente y proteccion de la reserva.";
        }

        if (hasStrongObservation && hasHighPrice)
        {
            return "La estrategia sugerida es una renegociacion fuerte: combinar lo observado en la visita con el precio por encima del valor justo estimado para justificar una oferta mas prudente o mejores condiciones antes de avanzar.";
        }

        if (hasStrongObservation || hasRelevantDocumentSignal || hasHighExpenses)
        {
            return "La estrategia sugerida es negociar con firmeza pero sin exagerar: pedir aclaraciones, documentacion de respaldo y usar las senales detectadas como argumentos concretos para mejorar precio o condiciones.";
        }

        if (hasMinorObservation || hasHighPrice)
        {
            return "La estrategia sugerida es una negociacion moderada: usar las observaciones cargadas y el rango de valor justo como referencia para ordenar la oferta y evitar pagar de mas.";
        }

        return "Con la informacion cargada, la estrategia sugerida es mantener una oferta alineada al valor justo estimado y pedir confirmaciones por escrito antes de asumir compromisos.";
    }

    private static PropertyReportNegotiationOpportunityDto? BuildVisitObservationOpportunity(string observation)
    {
        return observation switch
        {
            "Humedad" => new PropertyReportNegotiationOpportunityDto(
                "Humedad observada durante la visita",
                "La humedad observada puede justificar solicitar una revision profesional y usar ese punto como argumento para renegociar el precio o pedir reparaciones antes de avanzar.",
                "Alto",
                "Solicitar revision profesional y usarlo como argumento de renegociacion."),
            "Grietas" => new PropertyReportNegotiationOpportunityDto(
                "Grietas observadas durante la visita",
                "Las grietas observadas conviene aclararlas antes de firmar, especialmente si pueden estar vinculadas a mantenimiento, estructura o filtraciones.",
                "Alto",
                "Pedir evaluacion profesional o explicacion documentada antes de mejorar la oferta."),
            "Olor a humedad" => new PropertyReportNegotiationOpportunityDto(
                "Olor a humedad durante la visita",
                "El olor a humedad puede anticipar filtraciones o problemas de ventilacion que no siempre aparecen en las fotos o en la publicacion.",
                "Medio",
                "Solicitar una segunda visita y consultar por antecedentes de humedad o arreglos recientes."),
            "Problemas electricos" => new PropertyReportNegotiationOpportunityDto(
                "Problemas electricos observados",
                "Los problemas electricos observados pueden requerir revision tecnica antes de avanzar y sirven como argumento para pedir aclaraciones.",
                "Alto",
                "Pedir revision de instalacion electrica y dejar constancia antes de firmar."),
            "Problemas de agua" => new PropertyReportNegotiationOpportunityDto(
                "Problemas de agua observados",
                "Los problemas de agua pueden impactar el uso diario de la propiedad y conviene aclarar si son puntuales o recurrentes.",
                "Alto",
                "Pedir explicacion por escrito y revisar antecedentes de reclamos o reparaciones."),
            "Falta de ventilacion" => new PropertyReportNegotiationOpportunityDto(
                "Falta de ventilacion",
                "La falta de ventilacion puede afectar confort, humedad y mantenimiento futuro de la unidad.",
                "Medio",
                "Usarlo como argumento para negociar condiciones o pedir una revision mas detallada."),
            "Falta de luz natural" => new PropertyReportNegotiationOpportunityDto(
                "Falta de luz natural",
                "La falta de luz natural puede afectar el valor percibido y la comparacion con propiedades similares.",
                "Bajo",
                "Considerarlo al definir la oferta frente a comparables de la zona."),
            "Ruidos" => new PropertyReportNegotiationOpportunityDto(
                "Ruidos observados durante la visita",
                "Los ruidos pueden afectar el uso cotidiano y conviene verificarlos en distintos horarios antes de comprometer dinero.",
                "Medio",
                "Pedir otra visita en horario diferente y usarlo como punto de negociacion si se confirma."),
            "Ascensor deficiente" => new PropertyReportNegotiationOpportunityDto(
                "Ascensor deficiente",
                "Un ascensor deficiente puede anticipar gastos del consorcio o incomodidades de uso, especialmente en pisos altos.",
                "Medio",
                "Pedir informacion sobre mantenimiento, reparaciones pendientes y expensas extraordinarias."),
            "Fachada deteriorada" => new PropertyReportNegotiationOpportunityDto(
                "Fachada deteriorada",
                "La fachada deteriorada puede indicar mantenimiento pendiente del edificio y posibles gastos futuros del consorcio.",
                "Medio",
                "Pedir actas, presupuesto de obras y detalle de extraordinarias antes de ofertar."),
            "Otro" => new PropertyReportNegotiationOpportunityDto(
                "Observacion relevante de la visita",
                "La observacion cargada por el comprador puede servir para pedir aclaraciones o ajustar condiciones antes de avanzar.",
                "Medio",
                "Documentar la observacion y pedir respuesta por escrito antes de firmar."),
            _ => null
        };
    }

    private static bool IsStrongVisitObservation(string observation)
    {
        return observation is "Humedad" or "Grietas" or "Olor a humedad" or "Problemas electricos" or "Problemas de agua";
    }

    private static bool IsHumidityObservation(string observation)
    {
        return observation is "Humedad" or "Olor a humedad";
    }

    private static bool IsReservationFinding(PropertyAnalysis analysis, DocumentFinding finding)
    {
        return analysis.Documents.FirstOrDefault(item => item.Id == finding.PropertyDocumentId)?.DocumentType == DocumentType.ReservationAgreement;
    }

    private static bool MentionsHumidity(DocumentFinding finding)
    {
        var text = $"{finding.Title} {finding.Description} {finding.BuyerImpact}".ToLowerInvariant();
        return text.Contains("humedad") || text.Contains("filtracion") || text.Contains("filtraci");
    }

    private static bool IsNegotiationUsefulFinding(DocumentFinding finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.SuggestedNegotiationPoint))
        {
            return true;
        }

        if (MentionsHumidity(finding))
        {
            return true;
        }

        return finding.Category is FindingCategory.Building or FindingCategory.Financial or FindingCategory.Legal or FindingCategory.Negotiation;
    }

    private static string ImpactLabel(FindingSeverity severity)
    {
        return severity switch
        {
            FindingSeverity.Critical or FindingSeverity.High => "Alto",
            FindingSeverity.Medium => "Medio",
            _ => "Bajo"
        };
    }

    private static IReadOnlyCollection<string> BuildQuestionsForSellerOrAgent(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportExpensesSectionDto expensesSection)
    {
        var questions = new List<string>();
        questions.AddRange(Unique(findings.Select(item => item.SuggestedQuestion)));

        if (RequiresExpenseUnitConfirmation(expensesSection.DocumentConfidence?.Classification))
        {
            questions.Add("¿Podés confirmar por escrito que estas expensas corresponden a la unidad publicada?");
        }

        if (analysis.ChecklistItems.Any(item => item.IsRequired && !item.IsProvided))
        {
            questions.Add("¿Qué documentación falta entregar antes de avanzar con la reserva?");
        }

        if (analysis.MarketPosition is MarketPosition.Expensive or MarketPosition.Overpriced)
        {
            questions.Add("¿Hay margen para revisar el precio considerando el valor por m² de la zona?");
        }

        return WithFallback(questions, "¿Qué documentación y condiciones quedan pendientes antes de reservar?");
    }

    private static IReadOnlyCollection<string> BuildQuestionsForNotaryOrLawyer(
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportDocumentTypeSectionDto reservationSection)
    {
        var questions = new List<string>
        {
            "¿Hay documentación suficiente para avanzar con una reserva?",
            "¿La operación debería condicionarse al informe de dominio e inhibiciones?"
        };

        if (!reservationSection.IsAnalyzed || findings.Any(item => item.PropertyDocumentId != Guid.Empty &&
            item.Severity is FindingSeverity.High or FindingSeverity.Critical &&
            item.Category == FindingCategory.Legal))
        {
            questions.Add("¿La reserva protege al comprador si aparece un problema documental?");
            questions.Add("¿Conviene agregar una cláusula de devolución de reserva?");
        }

        return questions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyCollection<PropertyReportDocumentStatusDto> BuildReviewedDocuments(PropertyAnalysis analysis)
    {
        return analysis.Documents
            .Where(item => item.DocumentType is DocumentType.Expenses or DocumentType.ReservationAgreement)
            .GroupBy(item => item.DocumentType)
            .Select(group => group.OrderByDescending(item => item.AnalyzedAtUtc ?? item.UploadedAtUtc).First())
            .Select(document => new PropertyReportDocumentStatusDto(
                document.DocumentType.ToString(),
                DocumentTypeLabel(document.DocumentType),
                document.AnalysisStatus == DocumentAnalysisStatus.Completed ? "Analizado" : "Cargado, pendiente de análisis",
                document.AnalysisStatus == DocumentAnalysisStatus.Completed,
                false))
            .ToArray();
    }

    private static IReadOnlyCollection<PropertyReportDocumentStatusDto> BuildPendingDocuments(PropertyAnalysis analysis)
    {
        var reviewedTypes = analysis.Documents
            .Where(item => item.DocumentType is DocumentType.Expenses or DocumentType.ReservationAgreement)
            .Select(item => item.DocumentType)
            .ToHashSet();

        var currentPending = new[] { DocumentType.Expenses, DocumentType.ReservationAgreement }
            .Where(type => !reviewedTypes.Contains(type))
            .Select(type => new PropertyReportDocumentStatusDto(type.ToString(), DocumentTypeLabel(type), "Pendiente", false, false));

        var future = new[]
        {
            new PropertyReportDocumentStatusDto("AssemblyMinutes", "Actas de consorcio", "Próximamente en PropSegura", false, true),
            new PropertyReportDocumentStatusDto("FloorPlan", "Plano del departamento", "Próximamente en PropSegura", false, true),
            new PropertyReportDocumentStatusDto("DomainReport", "Informe de dominio e inhibiciones", "Próximamente en PropSegura", false, true),
            new PropertyReportDocumentStatusDto("Deed", "Escritura", "Próximamente en PropSegura", false, true)
        };

        return currentPending.Concat(future).ToArray();
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

        if (RequiresExpenseUnitConfirmation(expensesSection.DocumentConfidence?.Classification))
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

    private static bool IsMoneyRiskFinding(DocumentFinding finding)
    {
        if (finding.Category == FindingCategory.Financial)
        {
            return true;
        }

        var text = $"{finding.Title} {finding.Description}".ToLowerInvariant();
        return text.Contains("expensas") ||
            text.Contains("extraordinaria") ||
            text.Contains("moros") ||
            text.Contains("juicio") ||
            text.Contains("no reintegrable") ||
            text.Contains("penalidad") ||
            text.Contains("multa") ||
            text.Contains("devolucion") ||
            text.Contains("devolución");
    }

    private static string MoneyRiskWhyItMatters(DocumentFinding finding)
    {
        var text = $"{finding.Title} {finding.Description}".ToLowerInvariant();

        if (text.Contains("no reintegrable") || text.Contains("devolucion") || text.Contains("devolución"))
        {
            return "Puede afectar la posibilidad de recuperar la reserva si la operación no avanza.";
        }

        if (text.Contains("penalidad") || text.Contains("multa"))
        {
            return "Puede generar costos adicionales si alguna condición no se cumple.";
        }

        if (text.Contains("expensas"))
        {
            return "Puede cambiar el costo mensual real de la propiedad o revelar gastos pendientes.";
        }

        if (text.Contains("juicio") || text.Contains("moros"))
        {
            return "Puede anticipar costos futuros o conflictos del consorcio.";
        }

        return "Puede modificar el costo o las condiciones económicas de la operación.";
    }

    private static string? SourceDocumentType(PropertyAnalysis analysis, DocumentFinding finding)
    {
        return analysis.Documents.FirstOrDefault(item => item.Id == finding.PropertyDocumentId)?.DocumentType.ToString();
    }

    private static string? SourceDocumentLabel(PropertyAnalysis analysis, DocumentFinding finding)
    {
        var documentType = analysis.Documents.FirstOrDefault(item => item.Id == finding.PropertyDocumentId)?.DocumentType;
        return documentType.HasValue ? DocumentTypeLabel(documentType.Value) : null;
    }

    private static IReadOnlyCollection<string> WithFallback(List<string> items, string fallback)
    {
        var result = items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return result.Length == 0 ? [fallback] : result;
    }

    private static PropertyReportDocumentTypeSectionDto EmptyDocumentTypeSection(string status)
    {
        return new PropertyReportDocumentTypeSectionDto(false, false, status, null, [], [], []);
    }

    private static ExpenseBenchmarkAssessment BuildExpenseBenchmark(PropertyAnalysis analysis)
    {
        var benchmark = NeighborhoodBenchmark.Find(analysis.Neighborhood);
        decimal? monthlyExpensesPerSquareMeter = null;
        decimal? ratio = null;

        if (analysis.MonthlyExpenses.HasValue &&
            analysis.TotalSquareMeters.HasValue &&
            analysis.TotalSquareMeters.Value > 0)
        {
            monthlyExpensesPerSquareMeter = analysis.MonthlyExpenses.Value / analysis.TotalSquareMeters.Value;
        }

        if (monthlyExpensesPerSquareMeter.HasValue &&
            benchmark?.AverageMonthlyExpensesPerSquareMeterArs > 0)
        {
            ratio = monthlyExpensesPerSquareMeter.Value / benchmark.AverageMonthlyExpensesPerSquareMeterArs;
        }

        var comparison = ratio switch
        {
            null => "Sin benchmark de expensas",
            <= 0.85m => "Por debajo del benchmark de expensas",
            <= 1.10m => "En linea con el benchmark de expensas",
            <= 1.35m => "Por encima del benchmark de expensas",
            _ => "Muy por encima del benchmark de expensas"
        };

        return new ExpenseBenchmarkAssessment(
            monthlyExpensesPerSquareMeter,
            benchmark?.AverageMonthlyExpensesPerSquareMeterArs,
            ratio,
            comparison);
    }

    private static PropertyReportExpensesSectionDto EmptyExpensesSection(
        string status,
        PropertyAnalysis analysis,
        ExpenseBenchmarkAssessment expenseBenchmark)
    {
        var hasPreliminaryExpenseData = analysis.MonthlyExpenses.HasValue ||
            analysis.TotalSquareMeters.HasValue ||
            expenseBenchmark.BenchmarkMonthlyExpensesPerSquareMeter.HasValue;

        return new PropertyReportExpensesSectionDto(
            false,
            false,
            hasPreliminaryExpenseData ? BuildPreliminaryExpenseStatus(expenseBenchmark) : status,
            hasPreliminaryExpenseData ? "Preliminar - datos de publicacion" : "Sin datos de expensas",
            null,
            null,
            null,
            null,
            analysis.MonthlyExpenses,
            analysis.TotalSquareMeters,
            expenseBenchmark.MonthlyExpensesPerSquareMeter,
            expenseBenchmark.BenchmarkMonthlyExpensesPerSquareMeter,
            expenseBenchmark.Comparison,
            "Sin documento de expensas cargado",
            null,
            false,
            null,
            [],
            [],
            []);
    }

    private sealed record ExpenseBenchmarkAssessment(
        decimal? MonthlyExpensesPerSquareMeter,
        decimal? BenchmarkMonthlyExpensesPerSquareMeter,
        decimal? Ratio,
        string Comparison);

    private static IEnumerable<string> Unique(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<DocumentFinding> DistinctFindingsByStableCode(IEnumerable<DocumentFinding> findings)
    {
        return findings
            .GroupBy(finding => finding.ToStableCode(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(finding => finding.Severity)
                .ThenBy(finding => finding.CreatedAtUtc)
                .First())
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.CreatedAtUtc)
            .ToArray();
    }

    private static string BuildPublishedVsDocumentStatus(decimal? publishedMonthlyExpenses, decimal? detectedExpenseAmount)
    {
        if (!publishedMonthlyExpenses.HasValue && !detectedExpenseAmount.HasValue)
        {
            return "Sin valor publicado ni valor detectado";
        }

        if (!detectedExpenseAmount.HasValue)
        {
            return "Sin valor detectado en documento";
        }

        if (!publishedMonthlyExpenses.HasValue)
        {
            return "Valor detectado en documento, sin valor publicado para comparar";
        }

        if (publishedMonthlyExpenses.Value <= 0)
        {
            return "Valor publicado no valido para comparar";
        }

        var difference = Math.Abs(publishedMonthlyExpenses.Value - detectedExpenseAmount.Value) / publishedMonthlyExpenses.Value;

        return difference switch
        {
            <= 0.10m => "Documento alineado con el valor publicado",
            <= 0.25m => "Diferencia moderada entre publicacion y documento",
            _ => "Diferencia significativa entre publicacion y documento"
        };
    }

    private static bool RequiresExpenseUnitConfirmation(string? classification)
    {
        return classification is ExpenseDocumentValidator.UnitVerificationPendingClassification or "PartiallyValidated" or "NotValidated";
    }

    private static bool IsExpenseValueMismatch(DocumentFinding finding)
    {
        return finding.ToStableCode() == ExpenseDocumentValidator.ExpenseValueMismatchCode;
    }

    private static string BuildRiskSummary(DocumentFinding finding)
    {
        var why = finding.ToStableCode() switch
        {
            ExpenseDocumentValidator.ExpenseUnitMismatchCode =>
                "si el documento no corresponde a la unidad, el analisis de costos puede no representar la propiedad evaluada.",
            ExpenseDocumentValidator.ExpenseValueMismatchCode =>
                "si el monto real difiere del publicado, el costo mensual usado para decidir puede estar desactualizado.",
            "EXPENSE_DEBT_DETECTED" =>
                "puede anticipar deuda de la unidad o problemas de cobrabilidad del consorcio.",
            "EXPENSE_WORKS_DETECTED" =>
                "puede implicar cuotas, aumentos o gastos futuros del edificio.",
            "EXPENSE_ELEVATOR_REFERENCE" =>
                "puede anticipar mantenimiento o reparaciones relevantes del consorcio.",
            "EXPENSE_HUMIDITY_DETECTED" =>
                "puede implicar reparaciones, filtraciones recurrentes o necesidad de revision profesional.",
            _ => finding.BuyerImpact ?? finding.Description
        };

        return $"{finding.Title}: {why}";
    }

    private static bool HasExpenseOperatingSignals(IReadOnlyCollection<DocumentFindingDto> findings)
    {
        var signalCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EXPENSE_DEBT_DETECTED",
            "EXPENSE_WORKS_DETECTED",
            "EXPENSE_EXTRAORDINARY_DETECTED",
            "EXPENSE_HUMIDITY_DETECTED",
            "EXPENSE_ELEVATOR_REFERENCE",
            "EXPENSE_STAFF_COSTS_DETECTED"
        };

        return findings.Any(finding => signalCodes.Contains(finding.Code));
    }

    private static string BuildPreliminaryExpenseStatus(ExpenseBenchmarkAssessment expenseBenchmark)
    {
        return expenseBenchmark.Comparison switch
        {
            "Por debajo del benchmark de expensas" =>
                "Con los datos de la publicacion, las expensas parecen bajas para la zona y superficie.",
            "En linea con el benchmark de expensas" =>
                "Con los datos de la publicacion, las expensas parecen razonables para la zona y superficie.",
            "Por encima del benchmark de expensas" =>
                "Con los datos de la publicacion, las expensas parecen algo altas para la zona y superficie.",
            "Muy por encima del benchmark de expensas" =>
                "Con los datos de la publicacion, las expensas parecen altas para la zona y superficie.",
            _ => "Analisis preliminar de expensas basado en los datos cargados."
        };
    }

    private static string RecommendationLabel(OverallRecommendation recommendation)
    {
        return recommendation switch
        {
            OverallRecommendation.Advance => "Avanzar",
            OverallRecommendation.AdvanceWithCaution => "Avanzar negociando",
            OverallRecommendation.WaitForDocuments => "Resolver pendientes antes de avanzar",
            OverallRecommendation.Renegotiate => "Avanzar negociando",
            OverallRecommendation.ProfessionalReview => "Riesgo alto - revisar con profesional",
            _ => "Faltan datos"
        };
    }

    private static string RecommendationExplanation(OverallRecommendation recommendation)
    {
        return recommendation switch
        {
            OverallRecommendation.Advance => "La información disponible no muestra riesgos significativos.",
            OverallRecommendation.AdvanceWithCaution => "Existen elementos que pueden utilizarse para negociar mejores condiciones.",
            OverallRecommendation.WaitForDocuments => "Conviene completar documentación clave antes de avanzar con una reserva.",
            OverallRecommendation.Renegotiate => "Hay señales de precio, expensas o riesgos financieros que justifican negociar mejores condiciones.",
            OverallRecommendation.ProfessionalReview => "Se detectaron hallazgos relevantes que justifican revisión profesional específica.",
            _ => "Faltan datos básicos para emitir una recomendación confiable."
        };
    }

    private static string ReportStatusLabel(ReportStatus status)
    {
        return status switch
        {
            ReportStatus.Ready => "Informe listo",
            ReportStatus.NeedsReview => "Requiere revisión",
            _ => "Informe incompleto"
        };
    }

    private static string DocumentTypeLabel(DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.Expenses => "Expensas",
            DocumentType.ReservationAgreement => "Reserva",
            DocumentType.AssemblyMinutes => "Actas de consorcio",
            DocumentType.FloorPlan => "Plano del departamento",
            DocumentType.DomainReport => "Informe de dominio e inhibiciones",
            DocumentType.CoOwnershipRegulation => "Reglamento de copropiedad",
            _ => "Escritura"
        };
    }

    private static string RecommendedUseLabel(RecommendedUse recommendedUse)
    {
        return recommendedUse switch
        {
            RecommendedUse.Living => "Vivienda",
            RecommendedUse.Investment => "Inversión",
            RecommendedUse.Both => "Vivienda e inversión",
            RecommendedUse.NotRecommended => "Requiere mas analisis",
            _ => "Sin recomendación"
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
            MarketPosition.Overpriced => "Con sobreprecio",
            _ => "Sin datos"
        };
    }

    private static string FindingSeverityLabel(FindingSeverity severity)
    {
        return severity switch
        {
            FindingSeverity.Critical => "Crítico",
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
        ProfessionalReview = 5,
        InsufficientData = 6
    }

    private enum ReportStatus
    {
        Ready = 1,
        Incomplete = 2,
        NeedsReview = 3
    }
}
