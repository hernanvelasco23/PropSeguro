using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;

namespace PropSeguro.Application.PropertyAnalyses.GetPropertyAnalysis;

public sealed record GetPropertyAnalysisQuery(Guid Id) : IRequest<PropertyAnalysisDto?>;

public sealed class GetPropertyAnalysisQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetPropertyAnalysisQuery, PropertyAnalysisDto?>
{
    public async Task<PropertyAnalysisDto?> Handle(GetPropertyAnalysisQuery request, CancellationToken cancellationToken)
    {
        var analysis = await dbContext.PropertyAnalyses
            .Include(item => item.RiskFindings)
            .Include(item => item.ChecklistItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);

        return analysis?.ToDto();
    }
}
