using System.Globalization;
using System.Text;
using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Infrastructure.Documents;

public sealed class PdfOcrTextExtractor(
    IOptions<DocumentIntelligenceOptions> options,
    ILogger<PdfOcrTextExtractor> logger) : IPdfOcrTextExtractor
{
    private static readonly string[] AuditPageTerms =
    [
        "historial del documento",
        "signnow",
        "registro de auditoria"
    ];

    public async Task<PdfOcrResult> ExtractTextAsync(
        string filePath,
        DocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        var configurationError = ValidateOptions(options.Value);

        if (configurationError is not null)
        {
            logger.LogWarning(
                "Azure Document Intelligence OCR is not configured. FilePath={FilePath} Error={Error}",
                filePath,
                configurationError);

            return new PdfOcrResult(
                string.Empty,
                TextExtractionQuality.Empty,
                OcrAttempted: false,
                configurationError);
        }

        try
        {
            var model = string.IsNullOrWhiteSpace(options.Value.Model)
                ? "prebuilt-read"
                : options.Value.Model.Trim();
            var client = new DocumentIntelligenceClient(
                new Uri(options.Value.Endpoint!.Trim()),
                new AzureKeyCredential(options.Value.ApiKey!.Trim()));
            var documentBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                model,
                BinaryData.FromBytes(documentBytes),
                cancellationToken);
            var result = operation.Value;
            var pages = result.Pages
                .Select(page => new ExtractedPage(page.PageNumber, ExtractPageText(page, result.Content)))
                .ToArray();
            var ignoredPages = pages
                .Where(page => IsAuditPage(documentType, page.Text))
                .Select(page => page.PageNumber)
                .ToArray();
            var usefulPages = pages
                .Where(page => !string.IsNullOrWhiteSpace(page.Text))
                .Where(page => !ignoredPages.Contains(page.PageNumber))
                .ToArray();
            var combinedText = JoinPages(usefulPages.Select(page => page.Text));
            var quality = DocumentTextQualityEvaluator.Evaluate(documentType, combinedText);

            logger.LogInformation(
                "Azure Document Intelligence OCR completed. Model={Model} PageCount={PageCount} IgnoredPages={IgnoredPages} ExtractedCharacters={ExtractedCharacters}",
                model,
                pages.Length,
                ignoredPages.Length == 0 ? "(none)" : string.Join(",", ignoredPages),
                combinedText.Length);

            return new PdfOcrResult(
                combinedText,
                quality,
                OcrAttempted: true,
                string.IsNullOrWhiteSpace(combinedText)
                    ? "Azure Document Intelligence no pudo obtener texto util del documento. Revisalo y pega el texto manualmente antes de analizar."
                    : $"Texto extraido con Azure Document Intelligence ({model}). Paginas: {pages.Length}. Paginas ignoradas: {ignoredPages.Length}.");
        }
        catch (RequestFailedException exception)
        {
            logger.LogError(
                exception,
                "Azure Document Intelligence OCR failed. FilePath={FilePath} Model={Model} Status={Status} ErrorCode={ErrorCode} Exception={Exception}",
                filePath,
                options.Value.Model,
                exception.Status,
                exception.ErrorCode,
                exception.ToString());

            return new PdfOcrResult(
                string.Empty,
                TextExtractionQuality.Empty,
                OcrAttempted: true,
                $"Azure Document Intelligence no pudo procesar el documento. Status={exception.Status}, Code={exception.ErrorCode}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Azure Document Intelligence OCR failed. FilePath={FilePath} Model={Model} Exception={Exception}",
                filePath,
                options.Value.Model,
                exception.ToString());

            return new PdfOcrResult(
                string.Empty,
                TextExtractionQuality.Empty,
                OcrAttempted: true,
                "Azure Document Intelligence no pudo procesar el documento. Revisa la configuracion y los logs de App Service.");
        }
    }

    private static string? ValidateOptions(DocumentIntelligenceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return "Falta configurar DocumentIntelligence:Endpoint.";
        }

        if (!Uri.TryCreate(options.Endpoint.Trim(), UriKind.Absolute, out _))
        {
            return "DocumentIntelligence:Endpoint no es una URL valida.";
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return "Falta configurar DocumentIntelligence:ApiKey.";
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            return "Falta configurar DocumentIntelligence:Model. Usa prebuilt-read o prebuilt-layout.";
        }

        return null;
    }

    private static string ExtractPageText(DocumentPage page, string? documentContent)
    {
        var lineText = JoinPages(page.Lines
            .Select(line => line.Content)
            .Where(text => !string.IsNullOrWhiteSpace(text)));

        if (!string.IsNullOrWhiteSpace(lineText))
        {
            return lineText;
        }

        if (string.IsNullOrWhiteSpace(documentContent))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var span in page.Spans)
        {
            if (span.Offset < 0 || span.Length <= 0 || span.Offset >= documentContent.Length)
            {
                continue;
            }

            var length = Math.Min(span.Length, documentContent.Length - span.Offset);
            builder.AppendLine(documentContent.Substring(span.Offset, length).Trim());
        }

        return builder.ToString().Trim();
    }

    private static bool IsAuditPage(DocumentType documentType, string text)
    {
        var normalizedText = Normalize(text);

        if (AuditPageTerms.Any(term => normalizedText.Contains(Normalize(term), StringComparison.Ordinal)))
        {
            return true;
        }

        return DocumentTextQualityEvaluator.IsLikelyAuditPage(documentType, text);
    }

    private static string JoinPages(IEnumerable<string> pages)
    {
        return string.Join(
            "\n\n",
            pages
                .Select(page => page.Trim())
                .Where(page => !string.IsNullOrWhiteSpace(page)));
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record ExtractedPage(int PageNumber, string Text);
}
