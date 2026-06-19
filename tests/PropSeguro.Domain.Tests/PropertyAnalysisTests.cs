using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Domain.Tests;

public sealed class PropertyAnalysisTests
{
    [Fact]
    public void RunInitialAnalysis_generates_required_document_checklist()
    {
        var analysis = CreateAnalysis();

        analysis.RunInitialAnalysis();

        Assert.Equal(6, analysis.ChecklistItems.Count);
        Assert.Contains(analysis.ChecklistItems, item => item.DocumentType == DocumentType.Expenses && item.Status == ChecklistStatus.Missing);
        Assert.Contains(analysis.ChecklistItems, item => item.DocumentType == DocumentType.AssemblyMinutes);
        Assert.Contains(analysis.ChecklistItems, item => item.DocumentType == DocumentType.CoOwnershipRegulation);
        Assert.Contains(analysis.ChecklistItems, item => item.DocumentType == DocumentType.DomainReport);
        Assert.Contains(analysis.ChecklistItems, item => item.DocumentType == DocumentType.FloorPlan);
        Assert.Contains(analysis.ChecklistItems, item => item.DocumentType == DocumentType.ReservationAgreement);
    }

    [Fact]
    public void AddDocument_marks_matching_checklist_item_as_provided()
    {
        var analysis = CreateAnalysis();
        analysis.RunInitialAnalysis();

        analysis.AddDocument(Guid.NewGuid(), DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/test/original.pdf");

        var checklistItem = Assert.Single(analysis.ChecklistItems, item => item.DocumentType == DocumentType.Expenses);
        Assert.True(checklistItem.IsProvided);
        Assert.Equal(ChecklistStatus.Provided, checklistItem.Status);
        Assert.Single(analysis.Documents);
    }

    [Fact]
    public void AddDocument_improves_protection_score_without_changing_opportunity_score()
    {
        var analysis = CreateAnalysis(monthlyExpenses: null);
        analysis.RunInitialAnalysis();
        var initialProtectionScore = analysis.ProtectionScore;
        var initialOpportunityScore = analysis.OpportunityScore;

        analysis.AddDocument(Guid.NewGuid(), DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/test/original.pdf");

        Assert.True(analysis.ProtectionScore > initialProtectionScore);
        Assert.Equal(initialOpportunityScore, analysis.OpportunityScore);
    }

    [Fact]
    public void RunInitialAnalysis_calculates_opportunity_from_listing_data()
    {
        var analysis = CreateAnalysis();

        analysis.RunInitialAnalysis();

        Assert.True(analysis.OpportunityScore > 0);
        Assert.Equal(InitialRiskLevel.Low, analysis.InitialRiskLevel);
        Assert.Equal(RecommendedUse.Both, analysis.RecommendedUse);
        Assert.Equal(3000m, analysis.UsdPerSquareMeter);
        Assert.Equal("En linea con el benchmark", analysis.BenchmarkComparison);
        Assert.Equal(MarketPosition.Fair, analysis.MarketPosition);
        Assert.NotNull(analysis.EstimatedFairValueMinUsd);
        Assert.NotNull(analysis.EstimatedFairValueMaxUsd);
        Assert.NotNull(analysis.SuggestedFirstOfferUsd);
        Assert.NotNull(analysis.SuggestedTargetPriceUsd);
        Assert.True(analysis.SuggestedFirstOfferUsd < analysis.SuggestedTargetPriceUsd);
        AssertValidNegotiationRange(analysis);
        Assert.Contains("promedio estimado", analysis.NegotiationExplanation);
    }

    [Fact]
    public void RunInitialAnalysis_marks_price_twenty_percent_above_benchmark_as_overpriced()
    {
        var analysis = CreateAnalysis(priceUsd: 230000);

        analysis.RunInitialAnalysis();

        Assert.Equal(MarketPosition.Overpriced, analysis.MarketPosition);
        Assert.True(analysis.NegotiationMarginPercent > 0);
        Assert.True(analysis.SuggestedFirstOfferUsd < analysis.SuggestedTargetPriceUsd);
        Assert.True(analysis.SuggestedTargetPriceUsd < analysis.PriceUsd);
        AssertValidNegotiationRange(analysis);
    }

    [Fact]
    public void RunInitialAnalysis_when_property_is_cheap_does_not_suggest_paying_above_asking_price()
    {
        var analysis = CreateAnalysis(priceUsd: 80000);

        analysis.RunInitialAnalysis();

        Assert.Equal(MarketPosition.Cheap, analysis.MarketPosition);
        Assert.True(analysis.EstimatedFairValueMinUsd > analysis.PriceUsd);
        Assert.True(analysis.SuggestedTargetPriceUsd <= analysis.PriceUsd);
        Assert.True(analysis.SuggestedFirstOfferUsd < analysis.SuggestedTargetPriceUsd);
        Assert.True(analysis.NegotiationMarginPercent >= 0);
        Assert.True(analysis.SuggestedTargetPriceUsd <= analysis.EstimatedFairValueMaxUsd);
    }

    [Fact]
    public void Negotiation_marks_recoleta_benchmark_price_as_fair()
    {
        var analysis = CreateAnalysis(priceUsd: 168000, neighborhood: "Recoleta");

        analysis.RunInitialAnalysis();

        Assert.Equal(2800m, analysis.UsdPerSquareMeter);
        Assert.Equal(MarketPosition.Fair, analysis.MarketPosition);
        Assert.Equal(163000m, analysis.SuggestedTargetPriceUsd);
        Assert.Equal(159600m, analysis.SuggestedFirstOfferUsd);
    }

    [Fact]
    public void Negotiation_marks_palermo_price_above_twenty_percent_as_overpriced()
    {
        var analysis = CreateAnalysis(priceUsd: 220000, neighborhood: "Palermo");

        analysis.RunInitialAnalysis();

        Assert.Equal(MarketPosition.Overpriced, analysis.MarketPosition);
        Assert.Equal(198000m, analysis.SuggestedTargetPriceUsd);
        Assert.Equal(184700m, analysis.SuggestedFirstOfferUsd);
        Assert.Equal(10m, analysis.NegotiationMarginPercent);
        AssertValidNegotiationRange(analysis);
    }

    [Fact]
    public void Negotiation_caps_target_at_fair_value_high_when_published_price_is_far_above_fair_value()
    {
        var analysis = CreateAnalysis(priceUsd: 99000, neighborhood: "Caballito", totalSquareMeters: 30);

        analysis.RunInitialAnalysis();

        Assert.Equal(MarketPosition.Overpriced, analysis.MarketPosition);
        Assert.Equal(70800m, analysis.EstimatedFairValueMinUsd);
        Assert.Equal(78200m, analysis.EstimatedFairValueMaxUsd);
        Assert.Equal(70800m, analysis.SuggestedFirstOfferUsd);
        Assert.Equal(78200m, analysis.SuggestedTargetPriceUsd);
        Assert.Equal(84400m, analysis.MaxRecommendedPriceUsd);
        Assert.Contains("renegociacion fuerte", analysis.NegotiationExplanation);
        AssertValidNegotiationRange(analysis);
    }

    [Fact]
    public void Negotiation_marks_price_ten_percent_below_benchmark_as_cheap()
    {
        var analysis = CreateAnalysis(priceUsd: 162000, neighborhood: "Palermo");

        analysis.RunInitialAnalysis();

        Assert.Equal(2700m, analysis.UsdPerSquareMeter);
        Assert.Equal(MarketPosition.Cheap, analysis.MarketPosition);
        Assert.Equal(159600m, analysis.SuggestedTargetPriceUsd);
        Assert.Equal(157100m, analysis.SuggestedFirstOfferUsd);
    }

    [Fact]
    public void Negotiation_without_square_meters_is_unknown()
    {
        var analysis = CreateAnalysis(totalSquareMeters: null);

        analysis.RunInitialAnalysis();

        Assert.Null(analysis.UsdPerSquareMeter);
        Assert.Equal(MarketPosition.Unknown, analysis.MarketPosition);
        Assert.Null(analysis.SuggestedFirstOfferUsd);
        Assert.Null(analysis.SuggestedTargetPriceUsd);
    }

    [Fact]
    public void Negotiation_without_neighborhood_is_unknown()
    {
        var analysis = CreateAnalysis(neighborhood: "");

        analysis.RunInitialAnalysis();

        Assert.Equal(MarketPosition.Unknown, analysis.MarketPosition);
        Assert.Null(analysis.SuggestedFirstOfferUsd);
        Assert.Null(analysis.SuggestedTargetPriceUsd);
    }

    [Theory]
    [InlineData(162000, MarketPosition.Cheap)]
    [InlineData(180000, MarketPosition.Fair)]
    [InlineData(204000, MarketPosition.Expensive)]
    [InlineData(220000, MarketPosition.Overpriced)]
    public void Negotiation_classifies_market_position(decimal priceUsd, MarketPosition expectedPosition)
    {
        var analysis = CreateAnalysis(priceUsd: priceUsd, neighborhood: "Palermo");

        analysis.RunInitialAnalysis();

        Assert.Equal(expectedPosition, analysis.MarketPosition);
    }

    [Fact]
    public void RunInitialAnalysis_without_benchmark_marks_negotiation_as_unknown()
    {
        var analysis = CreateAnalysis(neighborhood: "Barrio Inventado");

        analysis.RunInitialAnalysis();

        Assert.Equal(MarketPosition.Unknown, analysis.MarketPosition);
        Assert.Null(analysis.SuggestedFirstOfferUsd);
        Assert.Null(analysis.SuggestedTargetPriceUsd);
    }

    [Fact]
    public void AddDocument_requires_positive_size()
    {
        var analysis = CreateAnalysis();
        analysis.RunInitialAnalysis();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            analysis.AddDocument(Guid.NewGuid(), DocumentType.Expenses, "expensas.pdf", "application/pdf", 0, "storage/test/original.pdf"));
    }

    private static PropertyAnalysis CreateAnalysis(
        decimal? monthlyExpenses = 95000,
        decimal priceUsd = 180000,
        string neighborhood = "Palermo",
        decimal? totalSquareMeters = 60)
    {
        return new PropertyAnalysis(
            "Departamento 3 ambientes",
            "https://example.com/listing/123",
            "Av. Santa Fe 3200",
            neighborhood,
            priceUsd,
            monthlyExpenses,
            totalSquareMeters,
            54,
            3,
            2,
            1,
            12,
            8,
            null,
            null,
            true,
            true,
            true,
            PropertyCondition.Good,
            "Notas de prueba");
    }

    private static void AssertValidNegotiationRange(PropertyAnalysis analysis)
    {
        Assert.NotNull(analysis.EstimatedFairValueMaxUsd);
        Assert.NotNull(analysis.SuggestedFirstOfferUsd);
        Assert.NotNull(analysis.SuggestedTargetPriceUsd);
        Assert.NotNull(analysis.MaxRecommendedPriceUsd);

        Assert.True(analysis.SuggestedFirstOfferUsd <= analysis.SuggestedTargetPriceUsd);
        Assert.True(analysis.SuggestedTargetPriceUsd <= analysis.EstimatedFairValueMaxUsd);
        Assert.True(analysis.MaxRecommendedPriceUsd >= analysis.SuggestedTargetPriceUsd);
        Assert.True(analysis.MaxRecommendedPriceUsd <= analysis.EstimatedFairValueMaxUsd * 1.08m);
    }
}
