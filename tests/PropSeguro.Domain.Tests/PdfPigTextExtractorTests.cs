using PropSeguro.Infrastructure.Documents;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace PropSeguro.Domain.Tests;

public sealed class PdfPigTextExtractorTests
{
    [Fact]
    public async Task ExtractTextAsync_extracts_basic_text_from_pdf()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        try
        {
            var builder = new PdfDocumentBuilder();
            var page = builder.AddPage(PageSize.A4);
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            page.AddText("Expensas extraordinarias y deuda", 12, new PdfPoint(25, 700), font);
            await File.WriteAllBytesAsync(filePath, builder.Build());

            var extractor = new PdfPigTextExtractor();

            var text = await extractor.ExtractTextAsync(filePath, CancellationToken.None);

            Assert.Contains("Expensas", text);
            Assert.Contains("deuda", text);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
