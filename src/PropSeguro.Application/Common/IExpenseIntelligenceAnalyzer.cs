using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.Common;

public interface IExpenseIntelligenceAnalyzer
{
    ExpenseIntelligenceAnalysis Analyze(PropertyDocument document, string? text);
}

public sealed record ExpenseIntelligenceAnalysis(
    int BuildingHealthScore,
    string Classification,
    string Summary,
    IReadOnlyCollection<DocumentFinding> Findings,
    IReadOnlyCollection<string> SuggestedQuestions,
    IReadOnlyCollection<string> SuggestedNegotiationPoints);

