using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;

namespace PropSeguro.Application.PropertyAnalyses.DeletePropertyAnalysis;

public sealed record DeletePropertyAnalysisCommand(Guid Id) : IRequest<bool>;

public sealed class DeletePropertyAnalysisCommandHandler : IRequestHandler<DeletePropertyAnalysisCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserContext? _currentUser;

    public DeletePropertyAnalysisCommandHandler(IApplicationDbContext dbContext, ICurrentUserContext? currentUser = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(DeletePropertyAnalysisCommand request, CancellationToken cancellationToken)
    {
        var analysis = await _dbContext.PropertyAnalyses
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);

        if (analysis is null || !_currentUser.CanAccess(analysis))
        {
            return false;
        }

        var documentFindings = await _dbContext.DocumentFindings
            .Where(item => item.PropertyAnalysisId == request.Id)
            .ToListAsync(cancellationToken);

        _dbContext.DocumentFindings.RemoveRange(documentFindings);
        _dbContext.PropertyAnalyses.Remove(analysis);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
