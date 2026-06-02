namespace PropSeguro.Domain.PropertyAnalyses;

public sealed class PropertyDocument
{
    private PropertyDocument()
    {
        FileName = string.Empty;
    }

    public PropertyDocument(DocumentType documentType, string fileName)
    {
        Id = Guid.NewGuid();
        DocumentType = documentType;
        FileName = string.IsNullOrWhiteSpace(fileName) ? throw new ArgumentException("File name is required.", nameof(fileName)) : fileName.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string FileName { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
