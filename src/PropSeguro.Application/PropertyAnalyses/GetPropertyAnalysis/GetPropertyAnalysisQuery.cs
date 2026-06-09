using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;

namespace PropSeguro.Application.PropertyAnalyses.GetPropertyAnalysis;

public sealed record GetPropertyAnalysisQuery(Guid Id) : IRequest<PropertyAnalysisDto?>;

public sealed class GetPropertyAnalysisQueryHandler : IRequestHandler<GetPropertyAnalysisQuery, PropertyAnalysisDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserContext? _currentUser;

    public GetPropertyAnalysisQueryHandler(IApplicationDbContext dbContext, ICurrentUserContext? currentUser = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PropertyAnalysisDto?> Handle(GetPropertyAnalysisQuery request, CancellationToken cancellationToken)
    {
        var analysis = await _dbContext.PropertyAnalyses
            .Include(item => item.Documents)
            .Include(item => item.RiskFindings)
            .Include(item => item.ChecklistItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);

        if (analysis is null || !_currentUser.CanAccess(analysis))
        {
            return null;
        }

        var documentFindings = await _dbContext.DocumentFindings
            .Where(item => item.PropertyAnalysisId == request.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        analysis.RecalculateOpportunityScore();

        return analysis.ToDto(documentFindings);
    }
}
