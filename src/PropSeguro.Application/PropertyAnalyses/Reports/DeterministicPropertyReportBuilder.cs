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

        analysis.RecalculateOpportunityScore();
        analysis.RecalculateProtectionScore(documentFindings);

        var recommendation = DetermineRecommendation(analysis, documentFindings);
        var reservationSection = BuildReservationSection(analysis, documentFindings);
        var expensesSection = BuildExpensesSection(analysis, documentFindings);
        var reportStatus = DetermineReportStatus(analysis, documentFindings);
        var generatedAtUtc = DateTime.UtcNow;
        var suggestedQuestions = Unique(documentFindings.Select(item => item.SuggestedQuestion)).ToArray();
        var suggestedNegotiationPoints = Unique(documentFindings.Select(item => item.SuggestedNegotiationPoint ?? item.SuggestedChange)).ToArray();

        return new PropertyReportDto(
            BuildPropertySummary(analysis),
            reportStatus.ToString(),
            ReportStatusLabel(reportStatus),
            BuildCoverSection(analysis, reportStatus, generatedAtUtc),
            BuildExecutiveSummary(analysis, recommendation),
            BuildExecutiveConclusion(analysis, recommendation, documentFindings),
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
            BuildWhatThisMeansForYou(analysis, documentFindings, reservationSection, expensesSection),
            BuildConditionsToAdvance(analysis, documentFindings, reservationSection, expensesSection),
            BuildMoneyRisks(analysis, documentFindings),
            BuildNegotiationArguments(analysis, documentFindings, expensesSection),
            BuildQuestionsForSellerOrAgent(analysis, documentFindings, expensesSection),
            BuildQuestionsForNotaryOrLawyer(documentFindings, reservationSection),
            BuildReviewedDocuments(analysis),
            BuildPendingDocuments(analysis),
            suggestedQuestions,
            suggestedNegotiationPoints,
            BuildNextSteps(analysis, documentFindings, reservationSection, expensesSection),
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
            item.Title == "Las expensas del documento no coinciden con la publicacion" ||
            item.Title == "Las expensas podrian corresponder a otra unidad") ||
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
                "Seguiría avanzando. La propiedad muestra una oportunidad razonable y la documentación analizada no presenta alertas graves con las reglas actuales.",
            OverallRecommendation.AdvanceWithCaution =>
                "Avanzaría con cautela. La propiedad puede ser interesante, pero todavía conviene resolver algunos puntos documentales antes de comprometer dinero.",
            OverallRecommendation.WaitForDocuments =>
                "Esperaría documentación. Antes de reservar, conviene completar los documentos clave y validar que no existan riesgos que cambien la decisión.",
            OverallRecommendation.Renegotiate =>
                "Renegociaría antes de avanzar. Hay señales de precio, expensas o riesgos financieros que pueden usarse para pedir mejores condiciones.",
            OverallRecommendation.Avoid =>
                "No avanzaría todavía. El análisis detectó riesgos relevantes o inconsistencias que deberían resolverse antes de firmar o transferir dinero.",
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

        if (expensesSection.DocumentConfidence?.Classification is "Mismatch")
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

        if (expensesSection.DocumentConfidence?.Classification is "Mismatch" or "PartiallyValidated" or "NotValidated")
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

        if (analysis.MonthlyExpenses.HasValue && analysis.PriceUsd.HasValue && analysis.MonthlyExpenses.Value > analysis.PriceUsd.Value * 0.01m)
        {
            risks.Add(new PropertyReportMoneyRiskDto(
                "Expensas altas frente al precio",
                "Un costo mensual alto puede afectar la rentabilidad o el presupuesto de vivienda.",
                "La propiedad puede ser más cara de sostener de lo que parece por precio publicado.",
                "Pedir detalle de expensas ordinarias, extraordinarias y deudas del consorcio.",
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

        if (expensesSection.DocumentConfidence?.Classification == "Mismatch")
        {
            arguments.Add("Expensas superiores o distintas a las publicadas.");
        }

        if (findings.Any(item => item.Title.Contains("extraordinarias", StringComparison.OrdinalIgnoreCase)))
        {
            arguments.Add("Expensas extraordinarias detectadas o pendientes de aclaración.");
        }

        if (analysis.ChecklistItems.Any(item => item.IsRequired && !item.IsProvided))
        {
            arguments.Add("Falta validación documental completa.");
        }

        if (analysis.MarketPosition is MarketPosition.Expensive or MarketPosition.Overpriced)
        {
            arguments.Add("Precio por m² por encima del promedio estimado para la zona.");
        }

        if (findings.Any(item => item.Category == FindingCategory.Financial || item.Category == FindingCategory.Documentation))
        {
            arguments.Add("Riesgos del edificio o de la documentación pendientes de aclaración.");
        }

        return WithFallback(arguments, "Todavía no hay argumentos fuertes de negociación detectados con la información cargada.");
    }

    private static IReadOnlyCollection<string> BuildQuestionsForSellerOrAgent(
        PropertyAnalysis analysis,
        IReadOnlyCollection<DocumentFinding> findings,
        PropertyReportExpensesSectionDto expensesSection)
    {
        var questions = new List<string>();
        questions.AddRange(Unique(findings.Select(item => item.SuggestedQuestion)));

        if (expensesSection.DocumentConfidence?.Classification is "Mismatch" or "PartiallyValidated" or "NotValidated")
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
            OverallRecommendation.Advance => "Seguiría avanzando",
            OverallRecommendation.AdvanceWithCaution => "Avanzaría con cautela",
            OverallRecommendation.WaitForDocuments => "Esperaría documentación",
            OverallRecommendation.Renegotiate => "Renegociaría",
            OverallRecommendation.Avoid => "No avanzaría",
            _ => "Faltan datos"
        };
    }

    private static string RecommendationExplanation(OverallRecommendation recommendation)
    {
        return recommendation switch
        {
            OverallRecommendation.Advance => "La oportunidad es buena y la protección documental es aceptable con las reglas actuales.",
            OverallRecommendation.AdvanceWithCaution => "La oportunidad parece buena, pero todavía hay riesgos o validaciones pendientes.",
            OverallRecommendation.WaitForDocuments => "Conviene completar documentación clave antes de avanzar con una reserva.",
            OverallRecommendation.Renegotiate => "Hay señales de precio, expensas o riesgos financieros que justifican renegociar.",
            OverallRecommendation.Avoid => "Hay riesgos severos o incompatibilidades que conviene resolver antes de seguir.",
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
            RecommendedUse.NotRecommended => "No recomendado",
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
        Avoid = 5,
        InsufficientData = 6
    }

    private enum ReportStatus
    {
        Ready = 1,
        Incomplete = 2,
        NeedsReview = 3
    }
}
