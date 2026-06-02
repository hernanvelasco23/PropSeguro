namespace PropSeguro.Domain.PropertyAnalyses;

public sealed class PropertyAnalysis
{
    private readonly List<PropertyDocument> _documents = [];
    private readonly List<RiskFinding> _riskFindings = [];
    private readonly List<BuyerChecklistItem> _checklistItems = [];

    private PropertyAnalysis()
    {
        Title = string.Empty;
        Address = string.Empty;
        Neighborhood = string.Empty;
    }

    public PropertyAnalysis(
        string title,
        string? listingUrl,
        string address,
        string neighborhood,
        decimal? priceUsd,
        decimal? monthlyExpenses,
        string? notes)
    {
        Id = Guid.NewGuid();
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Property title is required.", nameof(title)) : title.Trim();
        ListingUrl = NormalizeOptionalText(listingUrl);
        Address = string.IsNullOrWhiteSpace(address) ? throw new ArgumentException("Address is required.", nameof(address)) : address.Trim();
        Neighborhood = string.IsNullOrWhiteSpace(neighborhood) ? throw new ArgumentException("Neighborhood is required.", nameof(neighborhood)) : neighborhood.Trim();
        PriceUsd = priceUsd;
        MonthlyExpenses = monthlyExpenses;
        Notes = NormalizeOptionalText(notes);
        Status = AnalysisStatus.Created;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string? ListingUrl { get; private set; }
    public string Address { get; private set; }
    public string Neighborhood { get; private set; }
    public decimal? PriceUsd { get; private set; }
    public decimal? MonthlyExpenses { get; private set; }
    public string? Notes { get; private set; }
    public int RiskScore { get; private set; }
    public AnalysisStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<PropertyDocument> Documents => _documents;
    public IReadOnlyCollection<RiskFinding> RiskFindings => _riskFindings;
    public IReadOnlyCollection<BuyerChecklistItem> ChecklistItems => _checklistItems;

    public void RunInitialAnalysis()
    {
        _riskFindings.Clear();
        _checklistItems.Clear();

        if (!MonthlyExpenses.HasValue)
        {
            _riskFindings.Add(new RiskFinding(
                RiskFindingType.MissingDocumentation,
                RiskSeverity.Medium,
                "MISSING_EXPENSES",
                "Faltan las expensas mensuales",
                "Solicita las ultimas expensas para entender el costo mensual real y detectar deudas o ajustes pendientes."));
        }

        if (!PriceUsd.HasValue)
        {
            _riskFindings.Add(new RiskFinding(
                RiskFindingType.Risk,
                RiskSeverity.High,
                "MISSING_PRICE",
                "Falta el precio de la propiedad",
                "Sin precio no se puede comparar la operacion ni estimar el nivel de exposicion economica."));
        }

        if (string.IsNullOrWhiteSpace(ListingUrl))
        {
            _riskFindings.Add(new RiskFinding(
                RiskFindingType.Warning,
                RiskSeverity.Low,
                "MISSING_LISTING_URL",
                "No se informo link de publicacion",
                "El link ayuda a contrastar datos publicados contra la documentacion y lo conversado con la inmobiliaria."));
        }

        AddRequiredChecklistItems();
        RiskScore = CalculateRiskScore();
        Status = AnalysisStatus.Completed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void AddRequiredChecklistItems()
    {
        _checklistItems.Add(new BuyerChecklistItem("Ultimas expensas", ChecklistCategory.Documentation));
        _checklistItems.Add(new BuyerChecklistItem("Actas de asamblea", ChecklistCategory.Documentation));
        _checklistItems.Add(new BuyerChecklistItem("Reglamento de copropiedad", ChecklistCategory.Documentation));
        _checklistItems.Add(new BuyerChecklistItem("Informe de dominio", ChecklistCategory.Documentation));
        _checklistItems.Add(new BuyerChecklistItem("Plano", ChecklistCategory.Documentation));
    }

    private int CalculateRiskScore()
    {
        var score = 10;

        foreach (var finding in _riskFindings)
        {
            score += finding.Severity switch
            {
                RiskSeverity.Low => 10,
                RiskSeverity.Medium => 25,
                RiskSeverity.High => 40,
                _ => 0
            };
        }

        return Math.Min(score, 100);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
