using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.PropertyAnalyses.ListPropertyAnalyses;

public sealed record ListPropertyAnalysesQuery : IRequest<IReadOnlyCollection<PropertyAnalysisSummaryDto>>;

public sealed class ListPropertyAnalysesQueryHandler : IRequestHandler<ListPropertyAnalysesQuery, IReadOnlyCollection<PropertyAnalysisSummaryDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserContext? _currentUser;

    public ListPropertyAnalysesQueryHandler(IApplicationDbContext dbContext, ICurrentUserContext? currentUser = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyCollection<PropertyAnalysisSummaryDto>> Handle(ListPropertyAnalysesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.PropertyAnalyses
            .Include(item => item.ChecklistItems)
            .AsNoTracking();

        if (_currentUser?.UserId is { } userId && !_currentUser.IsAdmin)
        {
            query = query.Where(item => item.UserId == userId);
        }

        var analyses = await query
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        var analysisIds = analyses.Select(item => item.Id).ToArray();
        var findings = await _dbContext.DocumentFindings
            .Where(item => analysisIds.Contains(item.PropertyAnalysisId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var findingsByAnalysis = findings
            .GroupBy(item => item.PropertyAnalysisId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return analyses
            .Select(analysis =>
            {
                analysis.RecalculateOpportunityScore();
                findingsByAnalysis.TryGetValue(analysis.Id, out var analysisFindings);
                analysisFindings ??= [];

                return new PropertyAnalysisSummaryDto(
                    analysis.Id,
                    analysis.Title,
                    analysis.ListingUrl,
                    analysis.Address,
                    analysis.Neighborhood,
                    analysis.PriceUsd,
                    analysis.MonthlyExpenses,
                    analysis.TotalSquareMeters,
                    analysis.UsdPerSquareMeter,
                    analysis.OpportunityScore,
                    analysis.ProtectionScore,
                    analysis.RecommendedUse.ToString(),
                    analysis.InitialRiskLevel.ToString(),
                    analysis.CreatedAtUtc,
                    analysis.UpdatedAtUtc,
                    analysis.ChecklistItems.Count(item => item.IsRequired && !item.IsProvided),
                    analysisFindings.Length,
                    analysisFindings.Count(item => item.Severity == FindingSeverity.Critical),
                    analysis.GetCoverImageUrl(),
                    analysis.ToNegotiationAnalysisDto());
            })
            .ToArray();
    }
}
