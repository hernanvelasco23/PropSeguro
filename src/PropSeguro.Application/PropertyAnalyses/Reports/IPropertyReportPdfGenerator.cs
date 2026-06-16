namespace PropSeguro.Application.PropertyAnalyses.Reports;

public interface IPropertyReportPdfGenerator
{
    byte[] Generate(PropertyReportDto report);
}
