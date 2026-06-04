using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;

namespace PropSeguro.Application.PropertyAnalyses.SavePropertyDocumentText;

public sealed record SavePropertyDocumentTextCommand(
    Guid PropertyAnalysisId,
    Guid PropertyDocumentId,
    string? EditedText) : IRequest<PropertyDocumentDto?>;

public sealed class SavePropertyDocumentTextCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<SavePropertyDocumentTextCommand, PropertyDocumentDto?>
{
    public async Task<PropertyDocumentDto?> Handle(SavePropertyDocumentTextCommand request, CancellationToken cancellationToken)
    {
        var document = await dbContext.PropertyDocuments
            .FirstOrDefaultAsync(
                item => item.Id == request.PropertyDocumentId && item.PropertyAnalysisId == request.PropertyAnalysisId,
                cancellationToken);

        if (document is null)
        {
            return null;
        }

        document.SaveEditedText(request.EditedText);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PropertyDocumentDto(
            document.Id,
            document.DocumentType.ToString(),
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.StoragePath,
            !string.IsNullOrWhiteSpace(document.ExtractedText),
            document.ExtractedText,
            document.EditedText,
            document.TextExtractionStatus.ToString(),
            document.UploadedAtUtc,
            document.Status.ToString(),
            document.AnalysisStatus.ToString(),
            document.AnalyzedAtUtc,
            document.AnalysisSummary);
    }
}
