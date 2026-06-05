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
        PropertyCondition = PropertyCondition.Unknown;
        RecommendedUse = RecommendedUse.Unknown;
        MarketPosition = MarketPosition.Unknown;
        BenchmarkComparison = "Sin benchmark";
        NegotiationExplanation = "Datos insuficientes para estimar un rango de negociacion.";
    }

    public PropertyAnalysis(
        string title,
        string? listingUrl,
        string address,
        string neighborhood,
        decimal? priceUsd,
        decimal? monthlyExpenses,
        decimal? totalSquareMeters,
        decimal? coveredSquareMeters,
        int? rooms,
        int? bedrooms,
        int? bathrooms,
        int? buildingAgeYears,
        int? floor,
        bool? hasBalcony,
        bool? hasAmenities,
        bool? isCreditEligible,
        PropertyCondition? propertyCondition,
        string? notes)
    {
        Id = Guid.NewGuid();
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Property title is required.", nameof(title)) : title.Trim();
        ListingUrl = NormalizeOptionalText(listingUrl);
        Address = string.IsNullOrWhiteSpace(address) ? throw new ArgumentException("Address is required.", nameof(address)) : address.Trim();
        Neighborhood = NormalizeOptionalText(neighborhood) ?? string.Empty;
        PriceUsd = priceUsd;
        MonthlyExpenses = monthlyExpenses;
        TotalSquareMeters = totalSquareMeters;
        CoveredSquareMeters = coveredSquareMeters;
        Rooms = rooms;
        Bedrooms = bedrooms;
        Bathrooms = bathrooms;
        BuildingAgeYears = buildingAgeYears;
        Floor = floor;
        HasBalcony = hasBalcony;
        HasAmenities = hasAmenities;
        IsCreditEligible = isCreditEligible;
        PropertyCondition = propertyCondition ?? PropertyCondition.Unknown;
        Notes = NormalizeOptionalText(notes);
        Status = AnalysisStatus.Created;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
        BenchmarkComparison = "Sin benchmark";
        MarketPosition = MarketPosition.Unknown;
        NegotiationExplanation = "Datos insuficientes para estimar un rango de negociacion.";
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string? ListingUrl { get; private set; }
    public string Address { get; private set; }
    public string Neighborhood { get; private set; }
    public decimal? PriceUsd { get; private set; }
    public decimal? MonthlyExpenses { get; private set; }
    public decimal? TotalSquareMeters { get; private set; }
    public decimal? CoveredSquareMeters { get; private set; }
    public int? Rooms { get; private set; }
    public int? Bedrooms { get; private set; }
    public int? Bathrooms { get; private set; }
    public int? BuildingAgeYears { get; private set; }
    public int? Floor { get; private set; }
    public bool? HasBalcony { get; private set; }
    public bool? HasAmenities { get; private set; }
    public bool? IsCreditEligible { get; private set; }
    public PropertyCondition PropertyCondition { get; private set; }
    public string? Notes { get; private set; }
    public int OpportunityScore { get; private set; }
    public int ProtectionScore { get; private set; }
    public InitialRiskLevel InitialRiskLevel { get; private set; }
    public RecommendedUse RecommendedUse { get; private set; }
    public decimal? UsdPerSquareMeter { get; private set; }
    public string BenchmarkComparison { get; private set; }
    public MarketPosition MarketPosition { get; private set; }
    public decimal? EstimatedFairValueMinUsd { get; private set; }
    public decimal? EstimatedFairValueMaxUsd { get; private set; }
    public decimal? SuggestedFirstOfferUsd { get; private set; }
    public decimal? SuggestedTargetPriceUsd { get; private set; }
    public decimal? NegotiationMarginPercent { get; private set; }
    public string NegotiationExplanation { get; private set; }
    public string? CoverImageFileName { get; private set; }
    public string? CoverImageContentType { get; private set; }
    public long? CoverImageSizeBytes { get; private set; }
    public string? CoverImageStoragePath { get; private set; }
    public DateTime? CoverImageUploadedAtUtc { get; private set; }
    public AnalysisStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<PropertyDocument> Documents => _documents;
    public IReadOnlyCollection<RiskFinding> RiskFindings => _riskFindings;
    public IReadOnlyCollection<BuyerChecklistItem> ChecklistItems => _checklistItems;

    public PropertyDocument AddDocument(Guid documentId, DocumentType documentType, string fileName, string contentType, long sizeBytes, string storagePath)
    {
        var document = new PropertyDocument(documentId, Id, documentType, fileName, contentType, sizeBytes, storagePath);
        _documents.Add(document);

        var matchingChecklistItem = _checklistItems.FirstOrDefault(item => item.DocumentType == documentType);
        matchingChecklistItem?.MarkProvided();

        RecalculateProtectionScore();
        UpdatedAtUtc = DateTime.UtcNow;

        return document;
    }

    public void SetCoverImage(string fileName, string contentType, long sizeBytes, string storagePath)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Cover image file name is required.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Cover image content type is required.", nameof(contentType));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Cover image size must be positive.");
        }

        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException("Cover image storage path is required.", nameof(storagePath));
        }

        CoverImageFileName = fileName.Trim();
        CoverImageContentType = contentType.Trim();
        CoverImageSizeBytes = sizeBytes;
        CoverImageStoragePath = storagePath.Trim();
        CoverImageUploadedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CoverImageUploadedAtUtc.Value;
    }

    public void UpdatePropertyData(
        string title,
        string? listingUrl,
        string address,
        string neighborhood,
        decimal? priceUsd,
        decimal? monthlyExpenses,
        decimal? totalSquareMeters,
        decimal? coveredSquareMeters,
        int? rooms,
        int? bedrooms,
        int? bathrooms,
        int? buildingAgeYears,
        int? floor,
        bool? hasBalcony,
        bool? hasAmenities,
        bool? isCreditEligible,
        PropertyCondition? propertyCondition,
        string? notes)
    {
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Property title is required.", nameof(title)) : title.Trim();
        ListingUrl = NormalizeOptionalText(listingUrl);
        Address = string.IsNullOrWhiteSpace(address) ? throw new ArgumentException("Address is required.", nameof(address)) : address.Trim();
        Neighborhood = NormalizeOptionalText(neighborhood) ?? string.Empty;
        PriceUsd = priceUsd;
        MonthlyExpenses = monthlyExpenses;
        TotalSquareMeters = totalSquareMeters;
        CoveredSquareMeters = coveredSquareMeters;
        Rooms = rooms;
        Bedrooms = bedrooms;
        Bathrooms = bathrooms;
        BuildingAgeYears = buildingAgeYears;
        Floor = floor;
        HasBalcony = hasBalcony;
        HasAmenities = hasAmenities;
        IsCreditEligible = isCreditEligible;
        PropertyCondition = propertyCondition ?? PropertyCondition.Unknown;
        Notes = NormalizeOptionalText(notes);

        RecalculateOpportunityScore();
        UpdatedAtUtc = DateTime.UtcNow;
    }

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
        RecalculateOpportunityScore();
        RecalculateProtectionScore();
        Status = AnalysisStatus.Completed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void AddRequiredChecklistItems()
    {
        _checklistItems.Add(new BuyerChecklistItem(Id, DocumentType.Expenses, "Ultimas expensas", "Comprobante o liquidacion reciente de expensas.", isRequired: true));
        _checklistItems.Add(new BuyerChecklistItem(Id, DocumentType.AssemblyMinutes, "Actas de asamblea", "Ultimas actas para revisar decisiones, obras y gastos extraordinarios.", isRequired: true));
        _checklistItems.Add(new BuyerChecklistItem(Id, DocumentType.CoOwnershipRegulation, "Reglamento de copropiedad", "Reglamento aplicable al edificio o consorcio.", isRequired: true));
        _checklistItems.Add(new BuyerChecklistItem(Id, DocumentType.DomainReport, "Informe de dominio", "Informe para validar titularidad, gravamenes e inhibiciones relevantes.", isRequired: true));
        _checklistItems.Add(new BuyerChecklistItem(Id, DocumentType.FloorPlan, "Plano", "Plano aprobado o documentacion equivalente de la unidad.", isRequired: true));
        _checklistItems.Add(new BuyerChecklistItem(Id, DocumentType.ReservationAgreement, "Reserva o sena", "Modelo o borrador de reserva antes de firmar o pagar.", isRequired: true));
    }

    public void RecalculateOpportunityScore()
    {
        var score = 50;
        UsdPerSquareMeter = CalculateUsdPerSquareMeter();

        if (PriceUsd.HasValue)
        {
            score += 8;
        }
        else
        {
            score -= 20;
        }

        if (TotalSquareMeters.HasValue)
        {
            score += 8;
        }
        else
        {
            score -= 20;
        }

        if (string.IsNullOrWhiteSpace(Neighborhood))
        {
            score -= 12;
        }

        var benchmark = NeighborhoodBenchmark.Find(Neighborhood);
        ApplyBenchmarkRules(benchmark, ref score);
        ApplyExpensesRules(ref score);
        ApplyPropertyQualityRules(ref score);
        ApplyLayoutRules(ref score);
        CalculateNegotiationRange(benchmark);

        OpportunityScore = Math.Clamp(score, 0, 100);
        InitialRiskLevel = OpportunityScore >= 75
            ? InitialRiskLevel.Low
            : OpportunityScore >= 45
                ? InitialRiskLevel.Medium
                : InitialRiskLevel.High;
        RecommendedUse = DetermineRecommendedUse(benchmark);
    }

    public void RecalculateProtectionScore(IEnumerable<DocumentFinding>? documentFindings = null, IEnumerable<PropertyDocument>? additionalDocuments = null)
    {
        var score = 100;

        var missingRequiredItems = _checklistItems.Count(item => item.IsRequired && !item.IsProvided);
        score -= missingRequiredItems * 10;

        foreach (var document in _documents.Concat(additionalDocuments ?? []))
        {
            score -= document.AnalysisStatus switch
            {
                DocumentAnalysisStatus.NotStarted => 4,
                DocumentAnalysisStatus.Pending => 4,
                DocumentAnalysisStatus.Failed => 12,
                _ => 0
            };
        }

        foreach (var finding in documentFindings ?? [])
        {
            score -= finding.Severity switch
            {
                FindingSeverity.Info => 0,
                FindingSeverity.Low => 5,
                FindingSeverity.Medium => 15,
                FindingSeverity.High => 30,
                FindingSeverity.Critical => 45,
                _ => 0
            };
        }

        ProtectionScore = Math.Clamp(score, 0, 100);
    }

    private decimal? CalculateUsdPerSquareMeter()
    {
        return PriceUsd.HasValue && TotalSquareMeters.HasValue && TotalSquareMeters.Value > 0
            ? Math.Round(PriceUsd.Value / TotalSquareMeters.Value, 2)
            : null;
    }

    private void ApplyBenchmarkRules(NeighborhoodBenchmark? benchmark, ref int score)
    {
        if (benchmark is null || !UsdPerSquareMeter.HasValue)
        {
            BenchmarkComparison = benchmark is null ? "Sin benchmark" : "Datos insuficientes";
            return;
        }

        var ratio = UsdPerSquareMeter.Value / benchmark.AverageUsdPerSquareMeter;

        if (ratio <= 0.9m)
        {
            BenchmarkComparison = "Por debajo del benchmark";
            score += 18;
        }
        else if (ratio <= 1.1m)
        {
            BenchmarkComparison = "En linea con el benchmark";
            score += 8;
        }
        else if (ratio <= 1.25m)
        {
            BenchmarkComparison = "Por encima del benchmark";
            score -= 12;
        }
        else
        {
            BenchmarkComparison = "Muy por encima del benchmark";
            score -= 25;
        }
    }

    private void ApplyExpensesRules(ref int score)
    {
        if (!MonthlyExpenses.HasValue || !PriceUsd.HasValue || PriceUsd.Value <= 0)
        {
            score -= 8;
            return;
        }

        var monthlyExpenseRatio = MonthlyExpenses.Value / PriceUsd.Value;

        if (monthlyExpenseRatio <= 0.004m)
        {
            score += 8;
        }
        else if (monthlyExpenseRatio <= 0.007m)
        {
            score += 2;
        }
        else if (monthlyExpenseRatio <= 0.01m)
        {
            score -= 8;
        }
        else
        {
            score -= 18;
        }
    }

    private void ApplyPropertyQualityRules(ref int score)
    {
        score += PropertyCondition switch
        {
            PropertyCondition.New => 12,
            PropertyCondition.Excellent => 10,
            PropertyCondition.Good => 6,
            PropertyCondition.NeedsRenovation => -20,
            _ => -4
        };

        if (BuildingAgeYears.HasValue)
        {
            score += BuildingAgeYears.Value switch
            {
                <= 10 => 6,
                <= 30 => 2,
                <= 50 => -4,
                _ => -10
            };
        }

        if (IsCreditEligible == true)
        {
            score += 8;
        }
        else if (IsCreditEligible == false)
        {
            score -= 8;
        }

        if (HasBalcony == true)
        {
            score += 4;
        }

        if (HasAmenities == true)
        {
            score += 4;
        }
    }

    private void ApplyLayoutRules(ref int score)
    {
        if (Rooms.HasValue)
        {
            score += Rooms.Value >= 2 ? 4 : -2;
        }

        if (Bedrooms.HasValue)
        {
            score += Bedrooms.Value >= 1 ? 4 : -4;
        }

        if (Bathrooms.HasValue)
        {
            score += Bathrooms.Value >= 1 ? 3 : -6;
        }
    }

    private void CalculateNegotiationRange(NeighborhoodBenchmark? benchmark)
    {
        EstimatedFairValueMinUsd = null;
        EstimatedFairValueMaxUsd = null;
        SuggestedFirstOfferUsd = null;
        SuggestedTargetPriceUsd = null;
        NegotiationMarginPercent = null;

        if (benchmark is null || !PriceUsd.HasValue || !TotalSquareMeters.HasValue || TotalSquareMeters.Value <= 0 || !UsdPerSquareMeter.HasValue)
        {
            MarketPosition = MarketPosition.Unknown;
            NegotiationExplanation = "Faltan precio, metros o benchmark del barrio para estimar un rango de negociacion.";
            return;
        }

        var ratio = UsdPerSquareMeter.Value / benchmark.AverageUsdPerSquareMeter;
        MarketPosition = ratio switch
        {
            > 1.20m => MarketPosition.Overpriced,
            > 1.10m => MarketPosition.Expensive,
            > 0.90m => MarketPosition.Fair,
            _ => MarketPosition.Cheap
        };

        var qualityAdjustment = CalculateFairValueAdjustment();
        var adjustedBenchmark = benchmark.AverageUsdPerSquareMeter * (1 + qualityAdjustment);
        var fairValue = adjustedBenchmark * TotalSquareMeters.Value;

        EstimatedFairValueMinUsd = RoundMoney(fairValue * 0.95m);
        EstimatedFairValueMaxUsd = RoundMoney(fairValue * 1.05m);

        var target = CalculateSuggestedTargetPrice();
        var firstOffer = CalculateSuggestedFirstOffer(target);

        SuggestedTargetPriceUsd = RoundMoney(target);
        SuggestedFirstOfferUsd = RoundMoney(firstOffer);
        NegotiationMarginPercent = Math.Max(0, Math.Round((PriceUsd.Value - SuggestedTargetPriceUsd.Value) / PriceUsd.Value * 100, 1));
        NegotiationExplanation = BuildNegotiationExplanation(benchmark, qualityAdjustment);
    }

    private decimal CalculateSuggestedTargetPrice()
    {
        return MarketPosition switch
        {
            MarketPosition.Cheap => PriceUsd!.Value * 0.985m,
            MarketPosition.Fair => PriceUsd!.Value * 0.97m,
            MarketPosition.Expensive => PriceUsd!.Value * 0.935m,
            MarketPosition.Overpriced => PriceUsd!.Value * 0.90m,
            _ => PriceUsd!.Value
        };
    }

    private decimal CalculateSuggestedFirstOffer(decimal target)
    {
        return MarketPosition switch
        {
            MarketPosition.Cheap => PriceUsd!.Value * 0.97m,
            MarketPosition.Fair => PriceUsd!.Value * 0.95m,
            MarketPosition.Expensive => PriceUsd!.Value * 0.91m,
            MarketPosition.Overpriced => PriceUsd!.Value * 0.86m,
            _ => target * 0.97m
        };
    }

    private decimal CalculateFairValueAdjustment()
    {
        var adjustment = 0m;

        adjustment += BuildingAgeYears switch
        {
            null => 0m,
            <= 10 => 0.03m,
            <= 30 => 0.01m,
            <= 50 => -0.025m,
            _ => -0.055m
        };

        adjustment += PropertyCondition switch
        {
            PropertyCondition.New => 0.05m,
            PropertyCondition.Excellent => 0.035m,
            PropertyCondition.Good => 0.01m,
            PropertyCondition.NeedsRenovation => -0.10m,
            _ => 0m
        };

        if (HasBalcony == true)
        {
            adjustment += 0.015m;
        }

        if (HasAmenities == true)
        {
            adjustment += 0.02m;
        }

        if (IsCreditEligible == true)
        {
            adjustment += 0.025m;
        }
        else if (IsCreditEligible == false)
        {
            adjustment -= 0.035m;
        }

        return Math.Clamp(adjustment, -0.18m, 0.14m);
    }

    private string BuildNegotiationExplanation(NeighborhoodBenchmark benchmark, decimal qualityAdjustment)
    {
        var differencePercent = UsdPerSquareMeter.HasValue
            ? Math.Round((UsdPerSquareMeter.Value / benchmark.AverageUsdPerSquareMeter - 1) * 100, 1)
            : 0m;
        var absoluteDifferencePercent = Math.Abs(differencePercent);
        var position = MarketPosition switch
        {
            MarketPosition.Cheap => "por debajo",
            MarketPosition.Fair => "en linea",
            MarketPosition.Expensive => "por encima",
            MarketPosition.Overpriced => "muy por encima",
            _ => "sin posicion clara"
        };
        var adjustmentText = qualityAdjustment switch
        {
            > 0.02m => "Los atributos de la unidad justifican un ajuste positivo del rango justo.",
            < -0.02m => "Gastos, antiguedad, estado o financiacion justifican descontar valor del rango justo.",
            _ => "Los atributos de la unidad no cambian demasiado el benchmark base."
        };

        if (MarketPosition == MarketPosition.Cheap)
        {
            return $"El valor publicado se encuentra aproximadamente un {absoluteDifferencePercent:N1}% {position} del promedio estimado para propiedades similares en {benchmark.Neighborhood} ({benchmark.AverageUsdPerSquareMeter:N0} USD/m2), por eso parece una oportunidad. {adjustmentText} El precio objetivo queda levemente por debajo del publicado para no poner en riesgo una oportunidad atractiva.";
        }

        return $"El valor publicado se encuentra aproximadamente un {absoluteDifferencePercent:N1}% {position} del promedio estimado para propiedades similares en {benchmark.Neighborhood} ({benchmark.AverageUsdPerSquareMeter:N0} USD/m2). {adjustmentText} Existe margen razonable de negociacion y la primera oferta queda por debajo del objetivo para dejar espacio de conversacion.";
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value / 100m, 0, MidpointRounding.AwayFromZero) * 100m;
    }

    private RecommendedUse DetermineRecommendedUse(NeighborhoodBenchmark? benchmark)
    {
        if (HasTooMuchMissingBasicData() || OpportunityScore < 35 || PropertyCondition == PropertyCondition.NeedsRenovation)
        {
            return RecommendedUse.NotRecommended;
        }

        var livingScore = 0;
        var investmentScore = 0;

        if (benchmark?.LivingDemandLevel == DemandLevel.High)
        {
            livingScore += 2;
        }

        if (benchmark?.InvestmentDemandLevel == DemandLevel.High)
        {
            investmentScore += 2;
        }

        if (BenchmarkComparison is "Por debajo del benchmark" or "En linea con el benchmark")
        {
            investmentScore += 2;
        }
        else if (BenchmarkComparison is "Muy por encima del benchmark")
        {
            livingScore -= 1;
            investmentScore -= 2;
        }

        if (MonthlyExpenses.HasValue && PriceUsd.HasValue && PriceUsd.Value > 0 && MonthlyExpenses.Value / PriceUsd.Value <= 0.007m)
        {
            livingScore += 1;
            investmentScore += 1;
        }

        if (PropertyCondition is PropertyCondition.New or PropertyCondition.Excellent or PropertyCondition.Good)
        {
            livingScore += 2;
            investmentScore += 1;
        }

        if (HasBalcony == true || HasAmenities == true)
        {
            livingScore += 1;
        }

        var living = livingScore >= 4;
        var investment = investmentScore >= 4;

        return (living, investment) switch
        {
            (true, true) => RecommendedUse.Both,
            (true, false) => RecommendedUse.Living,
            (false, true) => RecommendedUse.Investment,
            _ => RecommendedUse.Unknown
        };
    }

    private bool HasTooMuchMissingBasicData()
    {
        var missing = 0;
        missing += PriceUsd.HasValue ? 0 : 1;
        missing += TotalSquareMeters.HasValue ? 0 : 1;
        missing += MonthlyExpenses.HasValue ? 0 : 1;
        missing += Rooms.HasValue ? 0 : 1;
        missing += Bedrooms.HasValue ? 0 : 1;

        return missing >= 3;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
