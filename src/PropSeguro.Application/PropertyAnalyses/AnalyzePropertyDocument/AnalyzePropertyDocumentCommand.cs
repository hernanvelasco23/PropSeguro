using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.PropertyAnalyses.AnalyzePropertyDocument;

public sealed record AnalyzePropertyDocumentCommand(
    Guid PropertyAnalysisId,
    Guid PropertyDocumentId,
    string? ExtractedText) : IRequest<DocumentAnalysisResultDto?>;

public sealed class AnalyzePropertyDocumentCommandHandler(
    IApplicationDbContext dbContext,
    IDocumentIntelligenceAnalyzer documentIntelligenceAnalyzer)
    : IRequestHandler<AnalyzePropertyDocumentCommand, DocumentAnalysisResultDto?>
{
    public async Task<DocumentAnalysisResultDto?> Handle(AnalyzePropertyDocumentCommand request, CancellationToken cancellationToken)
    {
        var analysis = await dbContext.PropertyAnalyses
            .Include(item => item.Documents)
            .Include(item => item.RiskFindings)
            .Include(item => item.ChecklistItems)
            .FirstOrDefaultAsync(item => item.Id == request.PropertyAnalysisId, cancellationToken);

        if (analysis is null)
        {
            return null;
        }

        var document = await dbContext.PropertyDocuments
            .FirstOrDefaultAsync(item => item.Id == request.PropertyDocumentId && item.PropertyAnalysisId == request.PropertyAnalysisId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        var previousFindings = dbContext.DocumentFindings
            .Where(item => item.PropertyDocumentId == request.PropertyDocumentId);

        dbContext.DocumentFindings.RemoveRange(previousFindings);

        var textSelection = document.SelectTextForAnalysis(request.ExtractedText);
        var intelligenceResult = await documentIntelligenceAnalyzer.AnalyzeAsync(document, textSelection.Text, cancellationToken);
        var findings = intelligenceResult.Findings;
        document.CompleteAnalysis(textSelection.Text, textSelection.Source, findings);
        dbContext.DocumentFindings.AddRange(findings);

        var allFindings = await dbContext.DocumentFindings
            .Where(item => item.PropertyAnalysisId == request.PropertyAnalysisId && item.PropertyDocumentId != request.PropertyDocumentId)
            .ToListAsync(cancellationToken);

        allFindings.AddRange(findings);
        analysis.RecalculateProtectionScore(allFindings);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DocumentAnalysisResultDto(
            analysis.Id,
            document.Id,
            document.AnalysisStatus.ToString(),
            document.AnalysisSummary,
            intelligenceResult.Summary,
            intelligenceResult.RiskLevel,
            findings.Select(finding => finding.ToDto()).ToArray(),
            intelligenceResult.SuggestedQuestions,
            intelligenceResult.SuggestedNegotiationPoints,
            BuildBuyerFriendlyExplanation(document, findings),
            analysis.ProtectionScore);
    }

    private static string BuildBuyerFriendlyExplanation(PropertyDocument document, IReadOnlyCollection<DocumentFinding> findings)
    {
        var prefix = document.DocumentType == DocumentType.ReservationAgreement
            ? "La reserva es el primer compromiso economico de la operacion. Revisar estas senales puede ayudarte a pedir aclaraciones, negociar cambios o frenar antes de transferir dinero."
            : "El analisis revisa senales documentales relevantes para decidir como avanzar.";

        var riskText = findings.Count == 0
            ? "No aparecieron alertas con las reglas actuales, pero conviene revisar el documento completo."
            : "Las alertas encontradas indican puntos que conviene preguntar y negociar antes de avanzar.";

        return $"{prefix} {riskText} Esto no reemplaza la revision de un escribano o abogado.";
    }
}
