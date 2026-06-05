namespace PropSeguro.Infrastructure.Documents;

public sealed class PdfOcrOptions
{
    public string? PdfToPpmPath { get; set; }
    public string? TessDataPath { get; set; }
    public string Language { get; set; } = "spa+eng";
    public int Dpi { get; set; } = 200;
    public int MaxPages { get; set; } = 8;
}
