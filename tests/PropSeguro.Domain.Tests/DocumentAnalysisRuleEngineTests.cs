using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Domain.Tests;

public sealed class DocumentAnalysisRuleEngineTests
{
    [Fact]
    public void Expenses_with_extraordinary_expenses_creates_financial_finding()
    {
        var document = CreateDocument(DocumentType.Expenses);

        var findings = DocumentAnalysisRuleEngine.Analyze(document, "Se aprueban expensas extraordinarias.");

        Assert.Contains(findings, finding =>
            finding.Category == FindingCategory.Financial &&
            finding.Severity == FindingSeverity.Medium);
    }

    [Fact]
    public void Expenses_with_debt_or_morosidad_creates_financial_finding()
    {
        var document = CreateDocument(DocumentType.Expenses);

        var findings = DocumentAnalysisRuleEngine.Analyze(document, "Se informa deuda y morosidad en el consorcio.");

        Assert.Contains(findings, finding =>
            finding.Category == FindingCategory.Financial &&
            finding.Severity == FindingSeverity.High &&
            finding.Title == "Deuda o morosidad detectada" &&
            finding.BuyerImpact is not null);
    }

    [Fact]
    public void Expenses_with_humidity_or_filtration_creates_operational_finding()
    {
        var document = CreateDocument(DocumentType.Expenses);

        var findings = DocumentAnalysisRuleEngine.Analyze(document, "Se detecta humedad y filtracion en partes comunes.");

        Assert.Contains(findings, finding =>
            finding.Category == FindingCategory.Operational &&
            finding.Severity == FindingSeverity.Medium &&
            finding.MatchedText is not null);
    }

    [Fact]
    public void ReservationAgreement_with_non_refundable_text_creates_legal_finding()
    {
        var document = CreateDocument(DocumentType.ReservationAgreement);

        var findings = DocumentAnalysisRuleEngine.Analyze(document, "La reserva es no reintegrable. Comprador Juan DNI 1, vendedor Ana DNI 2. Domicilio calle falsa 123. Expensas, impuestos y servicios al dia. Sujeto a aprobacion crediticia y revision de documentacion satisfactoria. Plazo de entrega de documentacion completa.");

        Assert.Contains(findings, finding =>
            finding.Category == FindingCategory.Legal &&
            finding.Severity == FindingSeverity.High &&
            finding.Title == "Reserva no reintegrable" &&
            finding.SuggestedQuestion is not null &&
            finding.SuggestedChange is not null &&
            finding.SuggestedNegotiationPoint is not null &&
            finding.MatchedText is not null);
    }

    [Fact]
    public void ReservationAgreement_with_penalty_clause_creates_high_financial_finding()
    {
        var document = CreateDocument(DocumentType.ReservationAgreement);

        var findings = DocumentAnalysisRuleEngine.Analyze(document, "Se aplicara penalidad por incumplimiento. Comprador Juan DNI 1, vendedor Ana DNI 2. Domicilio calle falsa 123. Expensas, impuestos y servicios al dia. Sujeto a aprobacion crediticia y revision de documentacion satisfactoria. Plazo de entrega de documentacion completa.");

        Assert.Contains(findings, finding =>
            finding.Category == FindingCategory.Financial &&
            finding.Severity == FindingSeverity.High &&
            finding.Title == "Penalidad o multa detectada");
    }

    [Fact]
    public void ReservationAgreement_with_72_hour_clause_creates_medium_negotiation_finding()
    {
        var document = CreateDocument(DocumentType.ReservationAgreement);

        var findings = DocumentAnalysisRuleEngine.Analyze(document, "La oferta vence en 72 horas. Comprador Juan DNI 1, vendedor Ana DNI 2. Domicilio calle falsa 123. Expensas, impuestos y servicios al dia. Sujeto a aprobacion crediticia y revision de documentacion satisfactoria. Plazo de entrega de documentacion completa.");

        Assert.Contains(findings, finding =>
            finding.Category == FindingCategory.Negotiation &&
            finding.Severity == FindingSeverity.Medium &&
            finding.Title == "Plazo breve o perentorio");
    }

    [Fact]
    public void ReservationAgreement_without_mortgage_approval_condition_creates_high_financial_finding()
    {
        var document = CreateDocument(DocumentType.ReservationAgreement);

        var findings = DocumentAnalysisRuleEngine.Analyze(document, "Comprador Juan DNI 1, vendedor Ana DNI 2. Domicilio calle falsa 123. Expensas, impuestos y servicios al dia. Revision de documentacion satisfactoria. Plazo de entrega de documentacion completa.");

        Assert.Contains(findings, finding =>
            finding.Category == FindingCategory.Financial &&
            finding.Severity == FindingSeverity.High &&
            finding.Title == "Falta condicion por aprobacion crediticia");
    }

    [Fact]
    public void ReservationAgreement_without_document_review_condition_creates_high_documentation_finding()
    {
        var document = CreateDocument(DocumentType.ReservationAgreement);

        var findings = DocumentAnalysisRuleEngine.Analyze(document, "Comprador Juan DNI 1, vendedor Ana DNI 2. Domicilio calle falsa 123. Expensas, impuestos y servicios al dia. Sujeto a aprobacion crediticia. Plazo de entrega de documentacion completa.");

        Assert.Contains(findings, finding =>
            finding.Category == FindingCategory.Documentation &&
            finding.Severity == FindingSeverity.High &&
            finding.Title == "Falta condicion por revision documental");
    }

    [Fact]
    public void DomainReport_with_embargo_creates_critical_finding()
    {
        var document = CreateDocument(DocumentType.DomainReport);

        var findings = DocumentAnalysisRuleEngine.Analyze(document, "Consta embargo vigente.");

        Assert.Contains(findings, finding =>
            finding.Category == FindingCategory.Legal &&
            finding.Severity == FindingSeverity.Critical);
    }

    [Fact]
    public void Empty_text_creates_documentation_finding()
    {
        var document = CreateDocument(DocumentType.Expenses);

        var findings = DocumentAnalysisRuleEngine.Analyze(document, "");

        Assert.Contains(findings, finding =>
            finding.Category == FindingCategory.Documentation &&
            finding.Severity == FindingSeverity.Low);
    }

    private static PropertyDocument CreateDocument(DocumentType documentType)
    {
        return new PropertyDocument(Guid.NewGuid(), Guid.NewGuid(), documentType, "documento.pdf", "application/pdf", 1024, "storage/test/original.pdf");
    }
}
