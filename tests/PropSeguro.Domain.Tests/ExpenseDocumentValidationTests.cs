using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Domain.Tests;

public sealed class ExpenseDocumentValidationTests
{
    [Fact]
    public void Expense_document_same_unit_is_validated()
    {
        var analysis = CreateAnalysis(floor: 8, apartmentUnit: "B", monthlyExpenses: 100000);
        var document = CreateExpenseDocument(analysis);

        var result = ExpenseDocumentValidator.Validate(
            analysis,
            document,
            "Piso 8 Depto B Unidad funcional 12 Total a pagar $ 104.000");

        Assert.Equal("Validated", result.Classification);
        Assert.Equal(95, result.ConfidenceScore);
        Assert.DoesNotContain(result.Findings, finding => finding.Severity == FindingSeverity.High);
    }

    [Fact]
    public void Expense_document_different_unit_creates_high_finding()
    {
        var analysis = CreateAnalysis(floor: 8, apartmentUnit: "B");
        var document = CreateExpenseDocument(analysis);

        var result = ExpenseDocumentValidator.Validate(analysis, document, "Piso 7 Depto A Total expensas $ 100.000");

        Assert.Equal("Mismatch", result.Classification);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == FindingSeverity.High &&
            finding.Title == "Las expensas podrian corresponder a otra unidad");
    }

    [Fact]
    public void Property_missing_unit_but_document_has_unit_creates_medium_finding()
    {
        var analysis = CreateAnalysis(floor: null, apartmentUnit: null, unitIdentifier: null);
        var document = CreateExpenseDocument(analysis);

        var result = ExpenseDocumentValidator.Validate(analysis, document, "Piso 8 Depto B Saldo a pagar $ 100.000");

        Assert.Equal("PartiallyValidated", result.Classification);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == FindingSeverity.Medium &&
            finding.Title == "No se pudo validar la unidad de las expensas");
    }

    [Fact]
    public void Document_missing_unit_creates_medium_finding()
    {
        var analysis = CreateAnalysis(floor: 8, apartmentUnit: "B");
        var document = CreateExpenseDocument(analysis);

        var result = ExpenseDocumentValidator.Validate(analysis, document, "Total a pagar $ 100.000");

        Assert.Equal("NotValidated", result.Classification);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == FindingSeverity.Medium &&
            finding.Title == "No se identifico la unidad en las expensas");
    }

    [Fact]
    public void Expense_amount_differs_more_than_fifteen_percent_creates_high_finding()
    {
        var analysis = CreateAnalysis(floor: 8, apartmentUnit: "B", monthlyExpenses: 100000);
        var document = CreateExpenseDocument(analysis);

        var result = ExpenseDocumentValidator.Validate(analysis, document, "Piso 8 Depto B Importe $ 130.000");

        Assert.Equal("Mismatch", result.Classification);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == FindingSeverity.High &&
            finding.Title == "Las expensas del documento no coinciden con la publicacion");
    }

    [Fact]
    public void Expense_amount_close_enough_does_not_create_mismatch()
    {
        var analysis = CreateAnalysis(floor: 8, apartmentUnit: "B", monthlyExpenses: 100000);
        var document = CreateExpenseDocument(analysis);

        var result = ExpenseDocumentValidator.Validate(analysis, document, "Piso 8 Depto B Expensas del mes $ 112.000");

        Assert.Equal("Validated", result.Classification);
        Assert.DoesNotContain(result.Findings, finding => finding.Title == "Las expensas del documento no coinciden con la publicacion");
    }

    [Fact]
    public void Existing_expense_risk_findings_still_work()
    {
        var document = new PropertyDocument(Guid.NewGuid(), Guid.NewGuid(), DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/test.pdf");

        var findings = DocumentAnalysisRuleEngine.Analyze(
            document,
            "Expensas extraordinarias por humedad, morosidad, ascensor y obras de fachada.").ToArray();

        Assert.Contains(findings, finding => finding.Title == "Expensas extraordinarias detectadas");
        Assert.Contains(findings, finding => finding.Title == "Humedad o filtracion detectada");
        Assert.Contains(findings, finding => finding.Title == "Morosidad o deuda detectada");
        Assert.Contains(findings, finding => finding.Title == "Referencia a ascensor");
        Assert.Contains(findings, finding => finding.Title == "Obras o reparaciones mencionadas");
    }

    private static PropertyDocument CreateExpenseDocument(PropertyAnalysis analysis)
    {
        return new PropertyDocument(Guid.NewGuid(), analysis.Id, DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/test.pdf");
    }

    private static PropertyAnalysis CreateAnalysis(int? floor, string? apartmentUnit, decimal? monthlyExpenses = 100000, string? unitIdentifier = "12")
    {
        var analysis = new PropertyAnalysis(
            "Departamento",
            "https://example.com",
            "Av. Santa Fe 3200",
            "Palermo",
            180000,
            monthlyExpenses,
            60,
            54,
            3,
            2,
            1,
            12,
            floor,
            apartmentUnit,
            unitIdentifier,
            true,
            true,
            true,
            PropertyCondition.Good,
            null);

        analysis.RunInitialAnalysis();
        return analysis;
    }
}
