using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;

namespace PropSeguro.Application.PropertyAnalyses.DeletePropertyAnalysis;

public sealed record DeletePropertyAnalysisCommand(Guid Id) : IRequest<bool>;

public sealed class DeletePropertyAnalysisCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<DeletePropertyAnalysisCommand, bool>
{
    public async Task<bool> Handle(DeletePropertyAnalysisCommand request, CancellationToken cancellationToken)
    {
        var analysis = await dbContext.PropertyAnalyses
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);

        if (analysis is null)
        {
            return false;
        }

        var documentFindings = await dbContext.DocumentFindings
            .Where(item => item.PropertyAnalysisId == request.Id)
            .ToListAsync(cancellationToken);

        dbContext.DocumentFindings.RemoveRange(documentFindings);
        dbContext.PropertyAnalyses.Remove(analysis);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
