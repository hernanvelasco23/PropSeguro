using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.Common;

public interface IDocumentIntelligenceAnalyzer
{
    Task<DocumentIntelligenceAnalysis> AnalyzeAsync(
        PropertyDocument document,
        string? text,
        CancellationToken cancellationToken = default);
}

public sealed record DocumentIntelligenceAnalysis(
    string DocumentType,
    string RiskLevel,
    string Summary,
    IReadOnlyCollection<DocumentFinding> Findings,
    IReadOnlyCollection<string> SuggestedQuestions,
    IReadOnlyCollection<string> SuggestedNegotiationPoints);
