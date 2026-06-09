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
    string? ExtractionError,
    TextExtractionQuality? TextExtractionQuality = null,
    string? ExtractionSummary = null) : IRequest<PropertyAnalysisDto?>;

public sealed class AddPropertyDocumentCommandHandler : IRequestHandler<AddPropertyDocumentCommand, PropertyAnalysisDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserContext? _currentUser;

    public AddPropertyDocumentCommandHandler(IApplicationDbContext dbContext, ICurrentUserContext? currentUser = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PropertyAnalysisDto?> Handle(AddPropertyDocumentCommand request, CancellationToken cancellationToken)
    {
        var analysis = await _dbContext.PropertyAnalyses
            .Include(item => item.Documents)
            .Include(item => item.RiskFindings)
            .Include(item => item.ChecklistItems)
            .FirstOrDefaultAsync(item => item.Id == request.PropertyAnalysisId, cancellationToken);

        if (analysis is null || !_currentUser.CanAccess(analysis))
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
            document.StoreExtractedText(
                request.ExtractedText,
                request.TextExtractionQuality,
                request.ExtractionSummary);
        }

        _dbContext.PropertyDocuments.Add(document);

        var checklistItem = analysis.ChecklistItems
            .FirstOrDefault(item => item.DocumentType == request.DocumentType);

        checklistItem?.MarkProvided();

        var documentFindings = await _dbContext.DocumentFindings
            .Where(item => item.PropertyAnalysisId == request.PropertyAnalysisId)
            .ToListAsync(cancellationToken);

        analysis.RecalculateProtectionScore(documentFindings, [document]);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var updatedAnalysis = await _dbContext.PropertyAnalyses
            .Include(item => item.Documents)
            .Include(item => item.RiskFindings)
            .Include(item => item.ChecklistItems)
            .AsNoTracking()
            .FirstAsync(item => item.Id == request.PropertyAnalysisId, cancellationToken);

        return updatedAnalysis.ToDto(documentFindings);
    }
}
