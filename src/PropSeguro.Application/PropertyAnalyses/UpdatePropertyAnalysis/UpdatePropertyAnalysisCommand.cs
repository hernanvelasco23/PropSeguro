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
    string? ApartmentUnit,
    string? UnitIdentifier,
    bool? HasBalcony,
    bool? HasAmenities,
    bool? IsCreditEligible,
    PropertyCondition? PropertyCondition,
    string? Notes,
    IReadOnlyCollection<string>? VisitObservations = null,
    string? VisitNotes = null) : IRequest<PropertyAnalysisDto?>;

public sealed class UpdatePropertyAnalysisCommandHandler : IRequestHandler<UpdatePropertyAnalysisCommand, PropertyAnalysisDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserContext? _currentUser;

    public UpdatePropertyAnalysisCommandHandler(IApplicationDbContext dbContext, ICurrentUserContext? currentUser = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PropertyAnalysisDto?> Handle(UpdatePropertyAnalysisCommand request, CancellationToken cancellationToken)
    {
        var analysis = await _dbContext.PropertyAnalyses
            .Include(item => item.Documents)
            .Include(item => item.RiskFindings)
            .Include(item => item.ChecklistItems)
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);

        if (analysis is null || !_currentUser.CanAccess(analysis))
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
            request.ApartmentUnit,
            request.UnitIdentifier,
            request.HasBalcony,
            request.HasAmenities,
            request.IsCreditEligible,
            request.PropertyCondition,
            request.Notes);

        if (request.VisitObservations is not null || request.VisitNotes is not null)
        {
            analysis.UpdateVisitObservations(request.VisitObservations, request.VisitNotes);
        }

        var documentFindings = await _dbContext.DocumentFindings
            .Where(item => item.PropertyAnalysisId == request.Id)
            .ToListAsync(cancellationToken);

        analysis.RecalculateProtectionScore(documentFindings);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return analysis.ToDto(documentFindings);
    }
}
