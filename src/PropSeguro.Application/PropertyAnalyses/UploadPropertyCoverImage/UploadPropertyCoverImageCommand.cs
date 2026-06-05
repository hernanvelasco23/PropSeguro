using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;

namespace PropSeguro.Application.PropertyAnalyses.UploadPropertyCoverImage;

public sealed record UploadPropertyCoverImageCommand(
    Guid PropertyAnalysisId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StoragePath) : IRequest<PropertyAnalysisDto?>;

public sealed class UploadPropertyCoverImageCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UploadPropertyCoverImageCommand, PropertyAnalysisDto?>
{
    public async Task<PropertyAnalysisDto?> Handle(UploadPropertyCoverImageCommand request, CancellationToken cancellationToken)
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

        analysis.SetCoverImage(
            request.FileName,
            request.ContentType,
            request.SizeBytes,
            request.StoragePath);

        var documentFindings = await dbContext.DocumentFindings
            .Where(item => item.PropertyAnalysisId == request.PropertyAnalysisId)
            .ToListAsync(cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return analysis.ToDto(documentFindings);
    }
}
