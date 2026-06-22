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
        TextExtractionQuality = TextExtractionQuality.Unknown;
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
    public TextExtractionQuality TextExtractionQuality { get; private set; }
    public LastAnalyzedTextSource? LastAnalyzedTextSource { get; private set; }
    public DocumentAnalysisStatus AnalysisStatus { get; private set; }
    public DateTime? AnalyzedAtUtc { get; private set; }
    public string? AnalysisSummary { get; private set; }
    public bool UserConfirmedMatchesProperty { get; private set; }
    public DateTime? UserConfirmedMatchesPropertyAtUtc { get; private set; }

    public void StoreExtractedText(
        string? extractedText,
        TextExtractionQuality? textExtractionQuality = null,
        string? analysisSummary = null)
    {
        ExtractedText = string.IsNullOrWhiteSpace(extractedText) ? null : extractedText.Trim();
        TextExtractionStatus = ExtractedText is null || ExtractedText.Length < MinimumUsefulExtractedTextLength
            ? TextExtractionStatus.Empty
            : TextExtractionStatus.Extracted;
        TextExtractionQuality = textExtractionQuality ?? ClassifyTextQuality(ExtractedText);
        AnalysisStatus = DocumentAnalysisStatus.NotStarted;
        AnalysisSummary = string.IsNullOrWhiteSpace(analysisSummary)
            ? TextExtractionStatus == TextExtractionStatus.Empty
                ? "No se pudo extraer texto util del PDF. Si es un escaneo, pega el texto manualmente antes de analizar."
                : "Texto extraido del PDF. Listo para revisar o analizar."
            : analysisSummary.Trim();
    }

    public void MarkExtractionFailed(string message)
    {
        ExtractedText = null;
        TextExtractionStatus = TextExtractionStatus.Failed;
        TextExtractionQuality = TextExtractionQuality.Empty;
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
        TextExtractionQuality = ClassifyTextQuality(EditedText);
        AnalysisStatus = DocumentAnalysisStatus.NotStarted;
        AnalysisSummary = EditedText is null
            ? "No hay texto disponible para analizar."
            : "Texto corregido guardado. Listo para analizar.";
    }

    public DocumentTextSelection SelectTextForAnalysis(string? requestText)
    {
        if (!string.IsNullOrWhiteSpace(EditedText))
        {
            return new DocumentTextSelection(EditedText, global::PropSeguro.Domain.PropertyAnalyses.LastAnalyzedTextSource.EditedText);
        }

        if (!string.IsNullOrWhiteSpace(requestText))
        {
            return new DocumentTextSelection(requestText.Trim(), global::PropSeguro.Domain.PropertyAnalyses.LastAnalyzedTextSource.ManualText);
        }

        return new DocumentTextSelection(ExtractedText, global::PropSeguro.Domain.PropertyAnalyses.LastAnalyzedTextSource.ExtractedText);
    }

    public void CompleteAnalysis(string? analyzedText, LastAnalyzedTextSource textSource, IReadOnlyCollection<DocumentFinding> findings)
    {
        if (textSource == global::PropSeguro.Domain.PropertyAnalyses.LastAnalyzedTextSource.ManualText)
        {
            SaveEditedText(analyzedText);
        }

        LastAnalyzedTextSource = textSource;
        AnalysisStatus = DocumentAnalysisStatus.Completed;
        AnalyzedAtUtc = DateTime.UtcNow;
        TextExtractionQuality = ClassifyTextQuality(analyzedText);
        AnalysisSummary = findings.Count == 0
            ? "Analisis completado sin hallazgos determinantes. Esto no reemplaza la revision de un escribano o abogado."
            : $"Analisis completado con {findings.Count} hallazgo(s). Esto no reemplaza la revision de un escribano o abogado.";
    }

    public IReadOnlyCollection<DocumentFinding> CompleteAnalysis(string? text)
    {
        var selection = SelectTextForAnalysis(text);
        var findings = DocumentAnalysisRuleEngine.Analyze(this, selection.Text);
        CompleteAnalysis(selection.Text, selection.Source, findings);

        return findings;
    }

    public void ConfirmMatchesProperty()
    {
        UserConfirmedMatchesProperty = true;
        UserConfirmedMatchesPropertyAtUtc = DateTime.UtcNow;
    }

    private static TextExtractionQuality ClassifyTextQuality(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TextExtractionQuality.Empty;
        }

        var length = text.Trim().Length;

        if (length >= 600)
        {
            return TextExtractionQuality.Good;
        }

        if (length >= 150)
        {
            return TextExtractionQuality.Partial;
        }

        return length >= MinimumUsefulExtractedTextLength
            ? TextExtractionQuality.Poor
            : TextExtractionQuality.Empty;
    }
}

public sealed record DocumentTextSelection(string? Text, LastAnalyzedTextSource Source);
