using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Application.PropertyAnalyses.ListPropertyAnalyses;
using PropSeguro.Application.PropertyAnalyses.UpdatePropertyAnalysis;
using PropSeguro.Domain.PropertyAnalyses;
using PropSeguro.Infrastructure.Persistence;

namespace PropSeguro.Domain.Tests;

public sealed class PropertyAnalysisDashboardTests
{
    [Fact]
    public async Task List_returns_property_analysis_summaries()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var analysis = CreateAnalysis(ownerId);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var handler = new ListPropertyAnalysesQueryHandler(dbContext, User(ownerId));

        var result = await handler.Handle(new ListPropertyAnalysesQuery(), CancellationToken.None);

        var summary = Assert.Single(result);
        Assert.Equal(analysis.Id, summary.Id);
        Assert.Equal(analysis.Title, summary.Title);
        Assert.Equal(analysis.OpportunityScore, summary.OpportunityScore);
        Assert.Equal(6, summary.MissingRequiredDocumentsCount);
    }

    [Fact]
    public async Task Update_recalculates_opportunity_score()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var analysis = CreateAnalysis(ownerId, priceUsd: 230000);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();
        var initialScore = analysis.OpportunityScore;

        var handler = new UpdatePropertyAnalysisCommandHandler(dbContext, User(ownerId));

        var result = await handler.Handle(new UpdatePropertyAnalysisCommand(
            analysis.Id,
            analysis.Title,
            analysis.ListingUrl,
            analysis.Address,
            analysis.Neighborhood,
            150000,
            analysis.MonthlyExpenses,
            analysis.TotalSquareMeters,
            analysis.CoveredSquareMeters,
            analysis.Rooms,
            analysis.Bedrooms,
            analysis.Bathrooms,
            analysis.BuildingAgeYears,
            analysis.Floor,
            analysis.ApartmentUnit,
            analysis.UnitIdentifier,
            analysis.HasBalcony,
            analysis.HasAmenities,
            analysis.IsCreditEligible,
            analysis.PropertyCondition,
            analysis.Notes), CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEqual(initialScore, result.OpportunityScore);
        Assert.Equal("Cheap", result.MarketPosition);
    }

    [Fact]
    public async Task Update_preserves_documents_and_findings()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var analysis = CreateAnalysis(ownerId);
        var document = new PropertyDocument(Guid.NewGuid(), analysis.Id, DocumentType.ReservationAgreement, "reserva.pdf", "application/pdf", 1024, "storage/test/original.pdf");
        var finding = new DocumentFinding(document.Id, analysis.Id, FindingSeverity.Critical, FindingCategory.Legal, "Reserva no reintegrable", "Texto riesgoso.");
        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.PropertyDocuments.Add(document);
        dbContext.DocumentFindings.Add(finding);
        await dbContext.SaveChangesAsync();

        var handler = new UpdatePropertyAnalysisCommandHandler(dbContext, User(ownerId));

        var result = await handler.Handle(new UpdatePropertyAnalysisCommand(
            analysis.Id,
            "Titulo actualizado",
            analysis.ListingUrl,
            analysis.Address,
            analysis.Neighborhood,
            analysis.PriceUsd,
            analysis.MonthlyExpenses,
            analysis.TotalSquareMeters,
            analysis.CoveredSquareMeters,
            analysis.Rooms,
            analysis.Bedrooms,
            analysis.Bathrooms,
            analysis.BuildingAgeYears,
            analysis.Floor,
            analysis.ApartmentUnit,
            analysis.UnitIdentifier,
            analysis.HasBalcony,
            analysis.HasAmenities,
            analysis.IsCreditEligible,
            analysis.PropertyCondition,
            analysis.Notes), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Documents);
        Assert.Single(result.DocumentFindings);
    }

    [Fact]
    public async Task List_summary_includes_missing_documents_and_finding_counts()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var analysis = CreateAnalysis(ownerId);
        var document = new PropertyDocument(Guid.NewGuid(), analysis.Id, DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/test/original.pdf");
        var finding = new DocumentFinding(document.Id, analysis.Id, FindingSeverity.Critical, FindingCategory.Financial, "Deuda", "Texto con deuda.");
        analysis.ChecklistItems.First(item => item.DocumentType == DocumentType.Expenses).MarkProvided();
        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.PropertyDocuments.Add(document);
        dbContext.DocumentFindings.Add(finding);
        await dbContext.SaveChangesAsync();

        var handler = new ListPropertyAnalysesQueryHandler(dbContext, User(ownerId));

        var result = await handler.Handle(new ListPropertyAnalysesQuery(), CancellationToken.None);

        var summary = Assert.Single(result);
        Assert.Equal(5, summary.MissingRequiredDocumentsCount);
        Assert.Equal(1, summary.FindingsCount);
        Assert.Equal(1, summary.CriticalFindingsCount);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static PropertyAnalysis CreateAnalysis(Guid ownerId, decimal priceUsd = 180000)
    {
        var analysis = new PropertyAnalysis(
            "Departamento",
            "https://example.com/listing",
            "Av. Santa Fe 3200",
            "Palermo",
            priceUsd,
            95000,
            60,
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
            null);

        analysis.RunInitialAnalysis();
        analysis.AssignOwner(ownerId);
        return analysis;
    }

    private static TestCurrentUserContext User(Guid userId)
    {
        return new TestCurrentUserContext(userId, isAdmin: false);
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public TestCurrentUserContext(Guid userId, bool isAdmin)
        {
            UserId = userId;
            IsAdmin = isAdmin;
        }

        public Guid? UserId { get; }

        public string? Email => "test@propsegura.local";

        public string? Role => IsAdmin ? "Admin" : "User";

        public bool IsAdmin { get; }
    }
}
