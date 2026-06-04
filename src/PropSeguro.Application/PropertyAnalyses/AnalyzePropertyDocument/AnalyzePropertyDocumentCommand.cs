using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.PropertyAnalyses.AnalyzePropertyDocument;

public sealed record AnalyzePropertyDocumentCommand(
    Guid PropertyAnalysisId,
    Guid PropertyDocumentId,
    string? ExtractedText) : IRequest<DocumentAnalysisResultDto?>;

public sealed class AnalyzePropertyDocumentCommandHandler(IApplicationDbContext dbContext)
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

        var effectiveText = string.IsNullOrWhiteSpace(request.ExtractedText)
            ? document.ExtractedText
            : request.ExtractedText;

        var findings = document.CompleteAnalysis(effectiveText);
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
                .Select(finding => finding.SuggestedChange)
                .OfType<string>()
                .Where(change => !string.IsNullOrWhiteSpace(change))
                .Distinct()
                .ToArray(),
            BuildBuyerFriendlyExplanation(document, findings),
            analysis.ProtectionScore);
    }

    private static string BuildSummary(PropertyDocument document, IReadOnlyCollection<DocumentFinding> findings)
    {
        if (document.DocumentType != DocumentType.ReservationAgreement)
        {
            return document.AnalysisSummary ?? "Analisis documental completado.";
        }

        return findings.Count == 0
            ? "No se detectaron clausulas riesgosas con las reglas actuales."
            : $"Se detectaron {findings.Count} senal(es) para revisar antes de firmar o pagar la reserva.";
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
