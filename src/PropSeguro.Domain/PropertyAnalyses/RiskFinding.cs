namespace PropSeguro.Domain.PropertyAnalyses;

public sealed class RiskFinding
{
    private RiskFinding()
    {
        Code = string.Empty;
        Title = string.Empty;
        Description = string.Empty;
    }

    public RiskFinding(RiskFindingType type, RiskSeverity severity, string code, string title, string description)
    {
        Id = Guid.NewGuid();
        Type = type;
        Severity = severity;
        Code = string.IsNullOrWhiteSpace(code) ? throw new ArgumentException("Finding code is required.", nameof(code)) : code.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Finding title is required.", nameof(title)) : title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("Finding description is required.", nameof(description)) : description.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public RiskFindingType Type { get; private set; }
    public RiskSeverity Severity { get; private set; }
    public string Code { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
