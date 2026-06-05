using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.Common;

public interface IPdfOcrTextExtractor
{
    Task<PdfOcrResult> ExtractTextAsync(
        string filePath,
        DocumentType documentType,
        CancellationToken cancellationToken = default);
}

public sealed record PdfOcrResult(
    string Text,
    TextExtractionQuality Quality,
    bool OcrAttempted,
    string? Warning);
