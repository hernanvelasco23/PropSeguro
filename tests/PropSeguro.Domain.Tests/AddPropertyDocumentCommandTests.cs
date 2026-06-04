using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.PropertyAnalyses.AddPropertyDocument;
using PropSeguro.Domain.PropertyAnalyses;
using PropSeguro.Infrastructure.Persistence;

namespace PropSeguro.Domain.Tests;

public sealed class AddPropertyDocumentCommandTests
{
    [Fact]
    public async Task Add_document_stores_metadata_and_marks_checklist_item_as_provided()
    {
        await using var dbContext = CreateDbContext();
        var analysis = CreateAnalysis();
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var documentId = Guid.NewGuid();
        var handler = new AddPropertyDocumentCommandHandler(dbContext);

        var result = await handler.Handle(new AddPropertyDocumentCommand(
            analysis.Id,
            documentId,
            DocumentType.Expenses,
            "expensas.pdf",
            "application/pdf",
            2048,
            "storage/property-analyses/test/documents/test/original.pdf",
            "Se informan expensas extraordinarias.",
            null), CancellationToken.None);

        Assert.NotNull(result);
        var document = Assert.Single(result.Documents);
        Assert.Equal(documentId, document.Id);
        Assert.Equal("expensas.pdf", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(2048, document.SizeBytes);
        Assert.Equal("storage/property-analyses/test/documents/test/original.pdf", document.StoragePath);
        Assert.True(document.HasExtractedText);
        Assert.Contains("expensas extraordinarias", document.ExtractedText);
        Assert.Equal(nameof(TextExtractionStatus.Extracted), document.TextExtractionStatus);
        Assert.Contains(result.ChecklistItems, item => item.DocumentType == nameof(DocumentType.Expenses) && item.IsProvided);
    }

    [Fact]
    public async Task Add_pdf_with_empty_extracted_text_marks_extraction_as_empty()
    {
        await using var dbContext = CreateDbContext();
        var analysis = CreateAnalysis();
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var handler = new AddPropertyDocumentCommandHandler(dbContext);

        var result = await handler.Handle(new AddPropertyDocumentCommand(
            analysis.Id,
            Guid.NewGuid(),
            DocumentType.ReservationAgreement,
            "reserva.pdf",
            "application/pdf",
            2048,
            "storage/property-analyses/test/documents/test/original.pdf",
            "",
            null), CancellationToken.None);

        Assert.NotNull(result);
        var document = Assert.Single(result.Documents);
        Assert.False(document.HasExtractedText);
        Assert.Null(document.ExtractedText);
        Assert.Equal(nameof(TextExtractionStatus.Empty), document.TextExtractionStatus);
    }

    [Fact]
    public async Task Add_non_pdf_document_does_not_store_extracted_text()
    {
        await using var dbContext = CreateDbContext();
        var analysis = CreateAnalysis();
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var handler = new AddPropertyDocumentCommandHandler(dbContext);

        var result = await handler.Handle(new AddPropertyDocumentCommand(
            analysis.Id,
            Guid.NewGuid(),
            DocumentType.Other,
            "notas.txt",
            "text/plain",
            128,
            "storage/property-analyses/test/documents/test/original.pdf",
            "Texto que no deberia venir de extractor PDF.",
            null), CancellationToken.None);

        Assert.NotNull(result);
        var document = Assert.Single(result.Documents);
        Assert.False(document.HasExtractedText);
        Assert.Null(document.ExtractedText);
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
