namespace PropSeguro.Application.PropertyAnalyses.Reports;

public interface IPropertyReportBuilder
{
    Task<PropertyReportDto?> BuildAsync(Guid propertyAnalysisId, CancellationToken cancellationToken = default);
}
