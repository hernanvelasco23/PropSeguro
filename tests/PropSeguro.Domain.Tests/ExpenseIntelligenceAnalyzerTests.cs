using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Domain.Tests;

public sealed class ExpenseIntelligenceAnalyzerTests
{
    [Fact]
    public void Expense_analysis_calculates_health_score_from_detected_risks()
    {
        var analyzer = new DeterministicExpenseIntelligenceAnalyzer();
        var document = CreateDocument();

        var result = analyzer.Analyze(document, "Expensas extraordinarias por humedad, juicio del consorcio, obra de fachada, morosidad y ascensor.");

        Assert.Equal(10, result.BuildingHealthScore);
        Assert.Equal("Riesgo elevado", result.Classification);
        Assert.Contains(result.Findings, finding => finding.Title == "Expensas extraordinarias detectadas");
        Assert.Contains(result.Findings, finding => finding.Title == "Humedad o filtracion detectada");
        Assert.Contains(result.Findings, finding => finding.Title == "Juicio o reclamo judicial detectado");
        Assert.Contains(result.Findings, finding => finding.Title == "Obras o reparaciones mencionadas");
        Assert.Contains(result.Findings, finding => finding.Title == "Morosidad o deuda detectada");
        Assert.Contains(result.Findings, finding => finding.Title == "Referencia a ascensor");
    }

    [Fact]
    public void Expense_analysis_classifies_good_health_when_only_elevator_is_found()
    {
        var analyzer = new DeterministicExpenseIntelligenceAnalyzer();
        var document = CreateDocument();

        var result = analyzer.Analyze(document, "Service mensual del ascensor.");

        Assert.Equal(95, result.BuildingHealthScore);
        Assert.Equal("Excelente", result.Classification);
    }

    [Fact]
    public void Expense_analysis_generates_human_summary()
    {
        var analyzer = new DeterministicExpenseIntelligenceAnalyzer();
        var document = CreateDocument();

        var result = analyzer.Analyze(document, "Se detectaron derramas y reparaciones en fachada.");

        Assert.Contains("expensas extraordinarias", result.Summary);
        Assert.Contains("reparaciones edilicias", result.Summary);
        Assert.NotEmpty(result.SuggestedQuestions);
        Assert.NotEmpty(result.SuggestedNegotiationPoints);
    }

    private static PropertyDocument CreateDocument()
    {
        return new PropertyDocument(Guid.NewGuid(), Guid.NewGuid(), DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/test/original.pdf");
    }
}

