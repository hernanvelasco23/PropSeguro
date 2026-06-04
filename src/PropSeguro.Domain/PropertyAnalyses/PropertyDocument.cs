namespace PropSeguro.Domain.PropertyAnalyses;

public sealed class PropertyDocument
{
    private const int MinimumUsefulExtractedTextLength = 30;

    private PropertyDocument()
    {
        FileName = string.Empty;
        ContentType = string.Empty;
        StoragePath = string.Empty;
    }

    public PropertyDocument(
        Guid id,
        Guid propertyAnalysisId,
        DocumentType documentType,
        string fileName,
        string contentType,
        long sizeBytes,
        string storagePath)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Document id is required.", nameof(id));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Document size must be greater than zero.");
        }

        Id = id;
        PropertyAnalysisId = propertyAnalysisId == Guid.Empty
            ? throw new ArgumentException("Property analysis id is required.", nameof(propertyAnalysisId))
            : propertyAnalysisId;
        DocumentType = documentType;
        FileName = string.IsNullOrWhiteSpace(fileName) ? throw new ArgumentException("File name is required.", nameof(fileName)) : fileName.Trim();
        ContentType = string.IsNullOrWhiteSpace(contentType) ? throw new ArgumentException("Content type is required.", nameof(contentType)) : contentType.Trim();
        SizeBytes = sizeBytes;
        StoragePath = string.IsNullOrWhiteSpace(storagePath) ? throw new ArgumentException("Storage path is required.", nameof(storagePath)) : storagePath.Trim();
        Status = DocumentStatus.Uploaded;
        TextExtractionStatus = TextExtractionStatus.NotStarted;
        AnalysisStatus = DocumentAnalysisStatus.NotStarted;
        UploadedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid PropertyAnalysisId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string FileName { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public string StoragePath { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public DocumentStatus Status { get; private set; }
    public string? ExtractedText { get; private set; }
    public string? EditedText { get; private set; }
    public TextExtractionStatus TextExtractionStatus { get; private set; }
    public DocumentAnalysisStatus AnalysisStatus { get; private set; }
    public DateTime? AnalyzedAtUtc { get; private set; }
    public string? AnalysisSummary { get; private set; }

    public void StoreExtractedText(string? extractedText)
    {
        ExtractedText = string.IsNullOrWhiteSpace(extractedText) ? null : extractedText.Trim();
        TextExtractionStatus = ExtractedText is null || ExtractedText.Length < MinimumUsefulExtractedTextLength
            ? TextExtractionStatus.Empty
            : TextExtractionStatus.Extracted;
        AnalysisStatus = DocumentAnalysisStatus.NotStarted;
        AnalysisSummary = TextExtractionStatus == TextExtractionStatus.Empty
            ? "No se pudo extraer texto util del PDF. Si es un escaneo, pega el texto manualmente antes de analizar."
            : "Texto extraido del PDF. Listo para revisar o analizar.";
    }

    public void MarkExtractionFailed(string message)
    {
        ExtractedText = null;
        TextExtractionStatus = TextExtractionStatus.Failed;
        AnalysisStatus = DocumentAnalysisStatus.Failed;
        AnalysisSummary = string.IsNullOrWhiteSpace(message)
            ? "No se pudo extraer texto del PDF."
            : message.Trim();
    }

    public void SaveEditedText(string? editedText)
    {
        EditedText = string.IsNullOrWhiteSpace(editedText) ? null : editedText.Trim();
        TextExtractionStatus = EditedText is null
            ? TextExtractionStatus.Empty
            : TextExtractionStatus.ManuallyEdited;
        AnalysisStatus = DocumentAnalysisStatus.NotStarted;
        AnalysisSummary = EditedText is null
            ? "No hay texto disponible para analizar."
            : "Texto corregido guardado. Listo para analizar.";
    }

    public IReadOnlyCollection<DocumentFinding> CompleteAnalysis(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            SaveEditedText(text);
        }

        var analysisText = !string.IsNullOrWhiteSpace(EditedText)
            ? EditedText
            : ExtractedText;

        AnalysisStatus = DocumentAnalysisStatus.Completed;
        AnalyzedAtUtc = DateTime.UtcNow;

        var findings = DocumentAnalysisRuleEngine.Analyze(this, analysisText);
        AnalysisSummary = findings.Count == 0
            ? "Analisis completado sin hallazgos determinantes. Esto no reemplaza la revision de un escribano o abogado."
            : $"Analisis completado con {findings.Count} hallazgo(s). Esto no reemplaza la revision de un escribano o abogado.";

        return findings;
    }
}
