namespace PropSeguro.Domain.PropertyAnalyses;

public sealed class BuyerChecklistItem
{
    private BuyerChecklistItem()
    {
        Title = string.Empty;
        Description = string.Empty;
    }

    public BuyerChecklistItem(Guid propertyAnalysisId, DocumentType documentType, string title, string description, bool isRequired)
    {
        Id = Guid.NewGuid();
        PropertyAnalysisId = propertyAnalysisId == Guid.Empty
            ? throw new ArgumentException("Property analysis id is required.", nameof(propertyAnalysisId))
            : propertyAnalysisId;
        DocumentType = documentType;
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Checklist title is required.", nameof(title)) : title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("Checklist description is required.", nameof(description)) : description.Trim();
        IsRequired = isRequired;
        IsProvided = false;
        Status = isRequired ? ChecklistStatus.Missing : ChecklistStatus.NotApplicable;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid PropertyAnalysisId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public bool IsRequired { get; private set; }
    public bool IsProvided { get; private set; }
    public ChecklistStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public void MarkProvided()
    {
        IsProvided = true;
        Status = ChecklistStatus.Provided;
    }
}
