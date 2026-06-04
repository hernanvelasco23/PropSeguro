using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.PropertyAnalyses.AddPropertyDocument;

public sealed record AddPropertyDocumentCommand(
    Guid PropertyAnalysisId,
    Guid PropertyDocumentId,
    DocumentType DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StoragePath,
    string? ExtractedText,
    string? ExtractionError) : IRequest<PropertyAnalysisDto?>;

public sealed class AddPropertyDocumentCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<AddPropertyDocumentCommand, PropertyAnalysisDto?>
{
    public async Task<PropertyAnalysisDto?> Handle(AddPropertyDocumentCommand request, CancellationToken cancellationToken)
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

        var document = new PropertyDocument(
            request.PropertyDocumentId,
            request.PropertyAnalysisId,
            request.DocumentType,
            request.FileName,
            request.ContentType,
            request.SizeBytes,
            request.StoragePath);

        if (!string.IsNullOrWhiteSpace(request.ExtractionError))
        {
            document.MarkExtractionFailed(request.ExtractionError);
        }
        else if (string.Equals(request.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            document.StoreExtractedText(request.ExtractedText);
        }

        dbContext.PropertyDocuments.Add(document);

        var checklistItem = analysis.ChecklistItems
            .FirstOrDefault(item => item.DocumentType == request.DocumentType);

        checklistItem?.MarkProvided();

        var documentFindings = await dbContext.DocumentFindings
            .Where(item => item.PropertyAnalysisId == request.PropertyAnalysisId)
            .ToListAsync(cancellationToken);

        analysis.RecalculateProtectionScore(documentFindings, [document]);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedAnalysis = await dbContext.PropertyAnalyses
            .Include(item => item.Documents)
            .Include(item => item.RiskFindings)
            .Include(item => item.ChecklistItems)
            .AsNoTracking()
            .FirstAsync(item => item.Id == request.PropertyAnalysisId, cancellationToken);

        return updatedAnalysis.ToDto(documentFindings);
    }
}
