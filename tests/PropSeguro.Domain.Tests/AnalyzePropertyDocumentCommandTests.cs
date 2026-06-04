using Microsoft.EntityFrameworkCore;
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

        var handler = new AnalyzePropertyDocumentCommandHandler(dbContext);

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

        var handler = new AnalyzePropertyDocumentCommandHandler(dbContext);

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

        var handler = new AnalyzePropertyDocumentCommandHandler(dbContext);

        var result = await handler.Handle(new AnalyzePropertyDocumentCommand(analysis.Id, document.Id, null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(result.Findings, finding =>
            finding.Category == nameof(FindingCategory.Financial) &&
            finding.Severity == nameof(FindingSeverity.Medium));
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

        var handler = new AnalyzePropertyDocumentCommandHandler(dbContext);

        var result = await handler.Handle(new AnalyzePropertyDocumentCommand(analysis.Id, document.Id, ""), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(result.Findings, finding =>
            finding.Category == nameof(FindingCategory.Documentation) &&
            finding.Severity == nameof(FindingSeverity.Low));
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
            true,
            true,
            true,
            PropertyCondition.Good,
            null);

        analysis.RunInitialAnalysis();
        return analysis;
    }
}
