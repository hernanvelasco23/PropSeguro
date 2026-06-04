namespace PropSeguro.Application.Common;

public interface IPdfTextExtractor
{
    Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken);
}
