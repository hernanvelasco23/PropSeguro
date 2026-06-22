using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.PropertyAnalyses.SavePropertyDocumentText;

public sealed record SavePropertyDocumentTextCommand(
    Guid PropertyAnalysisId,
    Guid PropertyDocumentId,
    string? EditedText) : IRequest<PropertyDocumentDto?>;

public sealed class SavePropertyDocumentTextCommandHandler : IRequestHandler<SavePropertyDocumentTextCommand, PropertyDocumentDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserContext? _currentUser;

    public SavePropertyDocumentTextCommandHandler(IApplicationDbContext dbContext, ICurrentUserContext? currentUser = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PropertyDocumentDto?> Handle(SavePropertyDocumentTextCommand request, CancellationToken cancellationToken)
    {
        var analysis = await _dbContext.PropertyAnalyses
            .FirstOrDefaultAsync(item => item.Id == request.PropertyAnalysisId, cancellationToken);

        if (analysis is null || !_currentUser.CanAccess(analysis))
        {
            return null;
        }

        var document = await _dbContext.PropertyDocuments
            .FirstOrDefaultAsync(
                item => item.Id == request.PropertyDocumentId && item.PropertyAnalysisId == request.PropertyAnalysisId,
                cancellationToken);

        if (document is null)
        {
            return null;
        }

        document.SaveEditedText(request.EditedText);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
            document.TextExtractionQuality.ToString(),
            document.LastAnalyzedTextSource?.ToString(),
            document.UploadedAtUtc,
            document.Status.ToString(),
            document.AnalysisStatus.ToString(),
            document.AnalyzedAtUtc,
            document.AnalysisSummary,
            document.UserConfirmedMatchesProperty,
            document.UserConfirmedMatchesPropertyAtUtc,
            document.DocumentType == DocumentType.Expenses
                ? ExpenseDocumentValidator.Validate(
                    analysis,
                    document,
                    document.EditedText ?? document.ExtractedText).ToDto()
                : null);
    }
}
