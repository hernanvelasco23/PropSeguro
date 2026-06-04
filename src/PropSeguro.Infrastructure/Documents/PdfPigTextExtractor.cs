using System.Text;
using PropSeguro.Application.Common;
using UglyToad.PdfPig;

namespace PropSeguro.Infrastructure.Documents;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = new StringBuilder();

            using var document = PdfDocument.Open(filePath);
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    text.AppendLine(page.Text);
                }
            }

            return text.ToString().Trim();
        }, cancellationToken);
    }
}
