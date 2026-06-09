using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.PropertyAnalyses.AnalyzePropertyDocument;

public sealed record AnalyzePropertyDocumentCommand(
    Guid PropertyAnalysisId,
    Guid PropertyDocumentId,
    string? ExtractedText) : IRequest<DocumentAnalysisResultDto?>;

public sealed class AnalyzePropertyDocumentCommandHandler : IRequestHandler<AnalyzePropertyDocumentCommand, DocumentAnalysisResultDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDocumentIntelligenceAnalyzer _documentIntelligenceAnalyzer;
    private readonly ICurrentUserContext? _currentUser;

    public AnalyzePropertyDocumentCommandHandler(
        IApplicationDbContext dbContext,
        IDocumentIntelligenceAnalyzer documentIntelligenceAnalyzer,
        ICurrentUserContext? currentUser = null)
    {
        _dbContext = dbContext;
        _documentIntelligenceAnalyzer = documentIntelligenceAnalyzer;
        _currentUser = currentUser;
    }

    public async Task<DocumentAnalysisResultDto?> Handle(AnalyzePropertyDocumentCommand request, CancellationToken cancellationToken)
    {
        var analysis = await _dbContext.PropertyAnalyses
            .Include(item => item.Documents)
            .Include(item => item.RiskFindings)
            .Include(item => item.ChecklistItems)
            .FirstOrDefaultAsync(item => item.Id == request.PropertyAnalysisId, cancellationToken);

        if (analysis is null || !_currentUser.CanAccess(analysis))
        {
            return null;
        }

        var document = await _dbContext.PropertyDocuments
            .FirstOrDefaultAsync(item => item.Id == request.PropertyDocumentId && item.PropertyAnalysisId == request.PropertyAnalysisId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        var previousFindings = _dbContext.DocumentFindings
            .Where(item => item.PropertyDocumentId == request.PropertyDocumentId);

        _dbContext.DocumentFindings.RemoveRange(previousFindings);

        var textSelection = document.SelectTextForAnalysis(request.ExtractedText);
        var intelligenceResult = await _documentIntelligenceAnalyzer.AnalyzeAsync(document, textSelection.Text, cancellationToken);
        var findings = intelligenceResult.Findings.ToList();
        ExpenseDocumentValidation? expenseValidation = null;

        if (document.DocumentType == DocumentType.Expenses)
        {
            expenseValidation = ExpenseDocumentValidator.Validate(analysis, document, textSelection.Text);
            findings.AddRange(expenseValidation.Findings);
        }

        document.CompleteAnalysis(textSelection.Text, textSelection.Source, findings);
        _dbContext.DocumentFindings.AddRange(findings);

        var allFindings = await _dbContext.DocumentFindings
            .Where(item => item.PropertyAnalysisId == request.PropertyAnalysisId && item.PropertyDocumentId != request.PropertyDocumentId)
            .ToListAsync(cancellationToken);

        allFindings.AddRange(findings);
        analysis.RecalculateProtectionScore(allFindings);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DocumentAnalysisResultDto(
            analysis.Id,
            document.Id,
            document.AnalysisStatus.ToString(),
            document.AnalysisSummary,
            BuildSummary(document, findings),
            BuildRiskLevel(findings),
            findings.Select(finding => finding.ToDto()).ToArray(),
            findings
                .Select(finding => finding.SuggestedQuestion)
                .OfType<string>()
                .Where(question => !string.IsNullOrWhiteSpace(question))
                .Distinct()
                .ToArray(),
            findings
                .Select(finding => finding.SuggestedNegotiationPoint ?? finding.SuggestedChange)
                .OfType<string>()
                .Where(point => !string.IsNullOrWhiteSpace(point))
                .Distinct()
                .ToArray(),
            BuildExpenseIntelligence(document, findings),
            expenseValidation.ToDto(),
            BuildBuyerFriendlyExplanation(document, findings),
            analysis.ProtectionScore);
    }

    private static string BuildSummary(PropertyDocument document, IReadOnlyCollection<DocumentFinding> findings)
    {
        if (findings.Count == 0)
        {
            return document.DocumentType == DocumentType.Expenses
                ? "No se detectaron senales relevantes en las expensas con las reglas actuales."
                : "Analisis documental completado sin hallazgos determinantes.";
        }

        return document.DocumentType == DocumentType.Expenses
            ? $"Se detectaron {findings.Count} senal(es) en expensas para preguntar o negociar antes de avanzar."
            : $"Se detectaron {findings.Count} hallazgo(s) documentales para revisar.";
    }

    private static string BuildRiskLevel(IReadOnlyCollection<DocumentFinding> findings)
    {
        if (findings.Any(finding => finding.Severity == FindingSeverity.Critical))
        {
            return "Critical";
        }

        if (findings.Any(finding => finding.Severity == FindingSeverity.High))
        {
            return "High";
        }

        if (findings.Any(finding => finding.Severity == FindingSeverity.Medium))
        {
            return "Medium";
        }

        if (findings.Any(finding => finding.Severity == FindingSeverity.Low))
        {
            return "Low";
        }

        return "Info";
    }

    private static ExpenseIntelligenceAnalysisDto? BuildExpenseIntelligence(
        PropertyDocument document,
        IReadOnlyCollection<DocumentFinding> findings)
    {
        if (document.DocumentType != DocumentType.Expenses)
        {
            return null;
        }

        var relevantFindings = findings
            .Where(finding => finding.Category != FindingCategory.Documentation)
            .ToArray();
        var score = DeterministicExpenseIntelligenceAnalyzer.CalculateBuildingHealthScore(relevantFindings);

        return new ExpenseIntelligenceAnalysisDto(
            score,
            DeterministicExpenseIntelligenceAnalyzer.Classify(score),
            findings.Count == 0
                ? "No se detectaron senales relevantes en las expensas con las reglas actuales."
                : $"Se detectaron {findings.Count} senal(es) en expensas para preguntar o negociar antes de avanzar.",
            findings.Select(finding => finding.ToDto()).ToArray(),
            findings
                .Select(finding => finding.SuggestedQuestion)
                .OfType<string>()
                .Where(question => !string.IsNullOrWhiteSpace(question))
                .Distinct()
                .ToArray(),
            findings
                .Select(finding => finding.SuggestedNegotiationPoint ?? finding.SuggestedChange)
                .OfType<string>()
                .Where(point => !string.IsNullOrWhiteSpace(point))
                .Distinct()
                .ToArray());
    }

    private static string BuildBuyerFriendlyExplanation(PropertyDocument document, IReadOnlyCollection<DocumentFinding> findings)
    {
        var prefix = document.DocumentType == DocumentType.ReservationAgreement
            ? "La reserva es el primer compromiso economico de la operacion. Revisar estas senales puede ayudarte a pedir aclaraciones, negociar cambios o frenar antes de transferir dinero."
            : document.DocumentType == DocumentType.Expenses
                ? "Las expensas pueden mostrar costos ocultos, problemas edilicios y riesgos que podrias heredar si compras la propiedad."
            : "El analisis revisa senales documentales relevantes para decidir como avanzar.";

        var riskText = findings.Count == 0
            ? "No aparecieron alertas con las reglas actuales, pero conviene revisar el documento completo."
            : "Las alertas encontradas indican puntos que conviene preguntar y negociar antes de avanzar.";

        return $"{prefix} {riskText} Esto no reemplaza la revision de un escribano o abogado.";
    }
}
