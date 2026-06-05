using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.Common;

public sealed class DeterministicDocumentIntelligenceAnalyzer : IDocumentIntelligenceAnalyzer
{
    public Task<DocumentIntelligenceAnalysis> AnalyzeAsync(
        PropertyDocument document,
        string? text,
        CancellationToken cancellationToken = default)
    {
        var findings = DocumentAnalysisRuleEngine.Analyze(document, text);
        var result = new DocumentIntelligenceAnalysis(
            document.DocumentType.ToString(),
            BuildRiskLevel(findings),
            BuildSummary(document, findings),
            findings,
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

        return Task.FromResult(result);
    }

    private static string BuildSummary(PropertyDocument document, IReadOnlyCollection<DocumentFinding> findings)
    {
        if (findings.Count == 0)
        {
            return document.DocumentType switch
            {
                DocumentType.ReservationAgreement => "No se detectaron clausulas riesgosas con las reglas actuales.",
                DocumentType.Expenses => "No se detectaron senales relevantes en las expensas con las reglas actuales.",
                _ => "Analisis documental completado sin hallazgos determinantes."
            };
        }

        return document.DocumentType switch
        {
            DocumentType.ReservationAgreement => $"Se detectaron {findings.Count} senal(es) para revisar antes de firmar o pagar la reserva.",
            DocumentType.Expenses => $"Se detectaron {findings.Count} senal(es) en expensas para preguntar o negociar antes de avanzar.",
            _ => $"Se detectaron {findings.Count} hallazgo(s) documentales para revisar."
        };
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
}
