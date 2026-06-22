using MediatR;
using PropSeguro.Application.Common;
using PropSeguro.Application.PropertyAnalyses;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.PropertyAnalyses.CreatePropertyAnalysis;

public sealed record CreatePropertyAnalysisCommand(
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
    string? VisitNotes = null) : IRequest<PropertyAnalysisDto>;

public sealed class CreatePropertyAnalysisCommandHandler : IRequestHandler<CreatePropertyAnalysisCommand, PropertyAnalysisDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserContext? _currentUser;

    public CreatePropertyAnalysisCommandHandler(IApplicationDbContext dbContext, ICurrentUserContext? currentUser = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PropertyAnalysisDto> Handle(CreatePropertyAnalysisCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Property title is required.", nameof(request.Title));
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            throw new ArgumentException("Address is required.", nameof(request.Address));
        }

        if (string.IsNullOrWhiteSpace(request.Neighborhood))
        {
            throw new ArgumentException("Neighborhood is required.", nameof(request.Neighborhood));
        }

        var analysis = new PropertyAnalysis(
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

        analysis.UpdateVisitObservations(request.VisitObservations, request.VisitNotes);

        if (_currentUser?.UserId is { } userId)
        {
            analysis.AssignOwner(userId);
        }

        analysis.RunInitialAnalysis();

        _dbContext.PropertyAnalyses.Add(analysis);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return analysis.ToDto();
    }
}
