using MediatR;
using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.PropertyAnalyses.UpdatePropertyAnalysis;

public sealed record UpdatePropertyAnalysisCommand(
    Guid Id,
    string Title,
    string? ListingUrl,
    string Address,
    string Neighborhood,
    decimal? PriceUsd,
    decimal? MonthlyExpenses,
    decimal? TotalSquareMeters,
    decimal? CoveredSquareMeters,
    int? Rooms,
    int? Bedrooms,
    int? Bathrooms,
    int? BuildingAgeYears,
    int? Floor,
    bool? HasBalcony,
    bool? HasAmenities,
    bool? IsCreditEligible,
    PropertyCondition? PropertyCondition,
    string? Notes) : IRequest<PropertyAnalysisDto?>;

public sealed class UpdatePropertyAnalysisCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdatePropertyAnalysisCommand, PropertyAnalysisDto?>
{
    public async Task<PropertyAnalysisDto?> Handle(UpdatePropertyAnalysisCommand request, CancellationToken cancellationToken)
    {
        var analysis = await dbContext.PropertyAnalyses
            .Include(item => item.Documents)
            .Include(item => item.RiskFindings)
            .Include(item => item.ChecklistItems)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);

        if (analysis is null)
        {
            return null;
        }

        analysis.UpdatePropertyData(
            request.Title,
            request.ListingUrl,
            request.Address,
            request.Neighborhood,
            request.PriceUsd,
            request.MonthlyExpenses,
            request.TotalSquareMeters,
            request.CoveredSquareMeters,
            request.Rooms,
            request.Bedrooms,
            request.Bathrooms,
            request.BuildingAgeYears,
            request.Floor,
            request.HasBalcony,
            request.HasAmenities,
            request.IsCreditEligible,
            request.PropertyCondition,
            request.Notes);

        var documentFindings = await dbContext.DocumentFindings
            .Where(item => item.PropertyAnalysisId == request.Id)
            .ToListAsync(cancellationToken);

        analysis.RecalculateProtectionScore(documentFindings);

        await dbContext.SaveChangesAsync(cancellationToken);

        return analysis.ToDto(documentFindings);
    }
}
