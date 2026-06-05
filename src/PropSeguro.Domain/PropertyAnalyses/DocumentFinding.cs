namespace PropSeguro.Domain.PropertyAnalyses;

public sealed class DocumentFinding
{
    private DocumentFinding()
    {
        Title = string.Empty;
        Description = string.Empty;
        SuggestedQuestion = string.Empty;
        SuggestedChange = string.Empty;
        BuyerImpact = string.Empty;
        SuggestedNegotiationPoint = string.Empty;
        MatchedText = string.Empty;
    }

    public DocumentFinding(
        Guid propertyDocumentId,
        Guid propertyAnalysisId,
        FindingSeverity severity,
        FindingCategory category,
        string title,
        string description,
        string? suggestedQuestion = null,
        string? suggestedChange = null,
        string? buyerImpact = null,
        string? suggestedNegotiationPoint = null,
        string? matchedText = null)
    {
        Id = Guid.NewGuid();
        PropertyDocumentId = propertyDocumentId == Guid.Empty
            ? throw new ArgumentException("Property document id is required.", nameof(propertyDocumentId))
            : propertyDocumentId;
        PropertyAnalysisId = propertyAnalysisId == Guid.Empty
            ? throw new ArgumentException("Property analysis id is required.", nameof(propertyAnalysisId))
            : propertyAnalysisId;
        Severity = severity;
        Category = category;
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Finding title is required.", nameof(title)) : title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("Finding description is required.", nameof(description)) : description.Trim();
        SuggestedQuestion = string.IsNullOrWhiteSpace(suggestedQuestion) ? null : suggestedQuestion.Trim();
        SuggestedChange = string.IsNullOrWhiteSpace(suggestedChange) ? null : suggestedChange.Trim();
        BuyerImpact = string.IsNullOrWhiteSpace(buyerImpact) ? null : buyerImpact.Trim();
        SuggestedNegotiationPoint = string.IsNullOrWhiteSpace(suggestedNegotiationPoint)
            ? SuggestedChange
            : suggestedNegotiationPoint.Trim();
        MatchedText = string.IsNullOrWhiteSpace(matchedText) ? null : matchedText.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid PropertyDocumentId { get; private set; }
    public Guid PropertyAnalysisId { get; private set; }
    public FindingSeverity Severity { get; private set; }
    public FindingCategory Category { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string? BuyerImpact { get; private set; }
    public string? SuggestedQuestion { get; private set; }
    public string? SuggestedChange { get; private set; }
    public string? SuggestedNegotiationPoint { get; private set; }
    public string? MatchedText { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
