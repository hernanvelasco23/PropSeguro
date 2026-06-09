using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Application.PropertyAnalyses.AnalyzePropertyDocument;
using PropSeguro.Domain.PropertyAnalyses;
using PropSeguro.Infrastructure.Persistence;

namespace PropSeguro.Domain.Tests;

public sealed class AnalyzePropertyDocumentCommandTests
{
    [Fact]
    public async Task Analyze_with_invalid_document_id_returns_null()
    {
        await using var dbContext = CreateDbContext();
        var analysis = CreateAnalysis();
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var handler = new AnalyzePropertyDocumentCommandHandler(dbContext, new DeterministicDocumentIntelligenceAnalyzer());

        var result = await handler.Handle(new AnalyzePropertyDocumentCommand(analysis.Id, Guid.NewGuid(), "texto"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Analyze_document_from_another_analysis_returns_null()
    {
        await using var dbContext = CreateDbContext();
        var firstAnalysis = CreateAnalysis();
        var secondAnalysis = CreateAnalysis();
        var document = new PropertyDocument(Guid.NewGuid(), secondAnalysis.Id, DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/test/original.pdf");

        dbContext.PropertyAnalyses.AddRange(firstAnalysis, secondAnalysis);
        dbContext.PropertyDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var handler = new AnalyzePropertyDocumentCommandHandler(dbContext, new DeterministicDocumentIntelligenceAnalyzer());

        var result = await handler.Handle(new AnalyzePropertyDocumentCommand(firstAnalysis.Id, document.Id, "deuda"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Analyze_uses_stored_extracted_text_when_request_text_is_empty()
    {
        await using var dbContext = CreateDbContext();
        var analysis = CreateAnalysis();
        var document = new PropertyDocument(Guid.NewGuid(), analysis.Id, DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/test/original.pdf");
        document.StoreExtractedText("Se aprueban expensas extraordinarias.");

        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.PropertyDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var handler = new AnalyzePropertyDocumentCommandHandler(dbContext, new DeterministicDocumentIntelligenceAnalyzer());

        var result = await handler.Handle(new AnalyzePropertyDocumentCommand(analysis.Id, document.Id, null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(result.Findings, finding =>
            finding.Category == nameof(FindingCategory.Financial) &&
            finding.Severity == nameof(FindingSeverity.High));
        Assert.NotNull(result.ExpenseIntelligence);
        Assert.Equal(80, result.ExpenseIntelligence.BuildingHealthScore);
    }

    [Fact]
    public async Task Analyze_without_any_extracted_text_creates_documentation_finding()
    {
        await using var dbContext = CreateDbContext();
        var analysis = CreateAnalysis();
        var document = new PropertyDocument(Guid.NewGuid(), analysis.Id, DocumentType.Expenses, "escaneado.pdf", "application/pdf", 1024, "storage/test/original.pdf");

        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.PropertyDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var handler = new AnalyzePropertyDocumentCommandHandler(dbContext, new DeterministicDocumentIntelligenceAnalyzer());

        var result = await handler.Handle(new AnalyzePropertyDocumentCommand(analysis.Id, document.Id, ""), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(result.Findings, finding =>
            finding.Category == nameof(FindingCategory.Documentation) &&
            finding.Severity == nameof(FindingSeverity.Low));
    }

    [Fact]
    public async Task Analyze_uses_edited_text_before_request_or_extracted_text()
    {
        await using var dbContext = CreateDbContext();
        var analysis = CreateAnalysis();
        var document = new PropertyDocument(Guid.NewGuid(), analysis.Id, DocumentType.ReservationAgreement, "reserva.pdf", "application/pdf", 1024, "storage/test/original.pdf");
        document.StoreExtractedText("Texto sin alertas. Comprador Juan DNI 1, vendedor Ana DNI 2. Domicilio calle falsa 123. Expensas, impuestos y servicios al dia. Sujeto a aprobacion crediticia y revision de documentacion satisfactoria. Plazo de entrega de documentacion completa.");
        document.SaveEditedText("La reserva es no reintegrable.");

        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.PropertyDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var handler = new AnalyzePropertyDocumentCommandHandler(dbContext, new DeterministicDocumentIntelligenceAnalyzer());

        var result = await handler.Handle(new AnalyzePropertyDocumentCommand(analysis.Id, document.Id, "texto manual sin alertas"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(result.Findings, finding => finding.Title == "Reserva no reintegrable");
        Assert.Equal(nameof(LastAnalyzedTextSource.EditedText), document.LastAnalyzedTextSource?.ToString());
    }

    [Fact]
    public async Task Analyze_manual_text_is_saved_as_edited_text()
    {
        await using var dbContext = CreateDbContext();
        var analysis = CreateAnalysis();
        var document = new PropertyDocument(Guid.NewGuid(), analysis.Id, DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/test/original.pdf");

        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.PropertyDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var handler = new AnalyzePropertyDocumentCommandHandler(dbContext, new DeterministicDocumentIntelligenceAnalyzer());

        var result = await handler.Handle(new AnalyzePropertyDocumentCommand(analysis.Id, document.Id, "Hay deuda y morosidad."), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Hay deuda y morosidad.", document.EditedText);
        Assert.Equal(nameof(LastAnalyzedTextSource.ManualText), document.LastAnalyzedTextSource?.ToString());
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static PropertyAnalysis CreateAnalysis()
    {
        var analysis = new PropertyAnalysis(
            "Departamento",
            "https://example.com",
            "Av. Santa Fe 3200",
            "Palermo",
            180000,
            95000,
            60,
            54,
            3,
            2,
            1,
            12,
            8,
            null,
            null,
            true,
            true,
            true,
            PropertyCondition.Good,
            null);

        analysis.RunInitialAnalysis();
        return analysis;
    }
}
