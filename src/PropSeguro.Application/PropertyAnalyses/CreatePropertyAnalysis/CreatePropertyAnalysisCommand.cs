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
    string? Notes) : IRequest<PropertyAnalysisDto>;

public sealed class CreatePropertyAnalysisCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreatePropertyAnalysisCommand, PropertyAnalysisDto>
{
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
            request.Notes);

        analysis.RunInitialAnalysis();

        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync(cancellationToken);

        return analysis.ToDto();
    }
}
