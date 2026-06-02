namespace PropSeguro.Domain.PropertyAnalyses;

public sealed class BuyerChecklistItem
{
    private BuyerChecklistItem()
    {
        Title = string.Empty;
    }

    public BuyerChecklistItem(string title, ChecklistCategory category)
    {
        Id = Guid.NewGuid();
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Checklist title is required.", nameof(title)) : title.Trim();
        Category = category;
        IsCompleted = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public ChecklistCategory Category { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
