using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Application.PropertyAnalyses.Reports;
using PropSeguro.Domain.PropertyAnalyses;
using PropSeguro.Infrastructure.Persistence;

namespace PropSeguro.Domain.Tests;

public sealed class PropertyReportBuilderTests
{
    [Fact]
    public async Task Report_is_generated_for_property()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();
        var builder = new DeterministicPropertyReportBuilder(dbContext, User(userId));

        var report = await builder.BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Equal(analysis.Title, report.PropertySummary.Title);
        Assert.Equal(DeterministicPropertyReportBuilder.DisclaimerText, report.Disclaimer);
    }

    [Fact]
    public async Task User_cannot_access_another_users_report()
    {
        await using var dbContext = CreateDbContext();
        var analysis = CreateAnalysis(Guid.NewGuid());
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();
        var builder = new DeterministicPropertyReportBuilder(dbContext, User(Guid.NewGuid()));

        var report = await builder.BuildAsync(analysis.Id);

        Assert.Null(report);
    }

    [Fact]
    public async Task Admin_can_access_any_report()
    {
        await using var dbContext = CreateDbContext();
        var analysis = CreateAnalysis(Guid.NewGuid());
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();
        var builder = new DeterministicPropertyReportBuilder(dbContext, User(Guid.NewGuid(), isAdmin: true));

        var report = await builder.BuildAsync(analysis.Id);

        Assert.NotNull(report);
    }

    [Fact]
    public async Task Critical_finding_leads_to_avoid()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        var document = analysis.AddDocument(Guid.NewGuid(), DocumentType.ReservationAgreement, "reserva.pdf", "application/pdf", 1024, "storage/reserva.pdf");
        var finding = new DocumentFinding(document.Id, analysis.Id, FindingSeverity.Critical, FindingCategory.Legal, "Reserva no reintegrable", "Clausula critica.");
        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.DocumentFindings.Add(finding);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Equal("Avoid", report.OverallRecommendation);
    }

    [Fact]
    public async Task Missing_documents_leads_to_wait_for_documents()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Equal("WaitForDocuments", report.OverallRecommendation);
    }

    [Fact]
    public async Task Expense_amount_mismatch_leads_to_renegotiate()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        MarkChecklistProvided(analysis);
        var reservation = analysis.AddDocument(Guid.NewGuid(), DocumentType.ReservationAgreement, "reserva.pdf", "application/pdf", 1024, "storage/reserva.pdf");
        reservation.CompleteAnalysis("Reserva revisada", LastAnalyzedTextSource.ManualText, []);
        var expenses = analysis.AddDocument(Guid.NewGuid(), DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/expensas.pdf");
        expenses.StoreExtractedText("Piso 8 Depto B Total a pagar $ 140.000");
        var finding = new DocumentFinding(
            expenses.Id,
            analysis.Id,
            FindingSeverity.High,
            FindingCategory.Financial,
            "Las expensas del documento no coinciden con la publicacion",
            "El valor detectado en el documento difiere significativamente del valor cargado o publicado.");
        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.DocumentFindings.Add(finding);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Equal("Renegotiate", report.OverallRecommendation);
    }

    [Fact]
    public async Task Good_opportunity_and_acceptable_protection_can_advance()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId, priceUsd: 115000, monthlyExpenses: 200);
        MarkChecklistProvided(analysis);
        var reservation = analysis.AddDocument(Guid.NewGuid(), DocumentType.ReservationAgreement, "reserva.pdf", "application/pdf", 1024, "storage/reserva.pdf");
        reservation.CompleteAnalysis("Reserva revisada", LastAnalyzedTextSource.ManualText, []);
        var expenses = analysis.AddDocument(Guid.NewGuid(), DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/expensas.pdf");
        expenses.StoreExtractedText("Piso 8 Depto B Total a pagar $ 200");
        expenses.CompleteAnalysis("Piso 8 Depto B Total a pagar $ 200", LastAnalyzedTextSource.ExtractedText, []);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Contains(report.OverallRecommendation, new[] { "Advance", "AdvanceWithCaution" });
    }

    [Fact]
    public async Task Report_does_not_flag_low_expenses_as_money_risk_when_below_neighborhood_benchmark()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId, priceUsd: 99000, monthlyExpenses: 30000);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Equal(500m, report.ExpensesSection.MonthlyExpensesPerSquareMeter);
        Assert.Equal(2800m, report.ExpensesSection.BenchmarkMonthlyExpensesPerSquareMeter);
        Assert.Equal("Por debajo del benchmark de expensas", report.ExpensesSection.ExpenseBenchmarkComparison);
        Assert.DoesNotContain(report.MoneyRisks, risk => risk.Title.Contains("Expensas altas", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.WhatThisMeansForYou, item => item.Contains("por debajo del benchmark mensual", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Report_flags_expenses_only_when_above_neighborhood_benchmark_per_square_meter()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId, priceUsd: 99000, monthlyExpenses: 240000);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Equal(4000m, report.ExpensesSection.MonthlyExpensesPerSquareMeter);
        Assert.Equal(2800m, report.ExpensesSection.BenchmarkMonthlyExpensesPerSquareMeter);
        Assert.Equal("Muy por encima del benchmark de expensas", report.ExpensesSection.ExpenseBenchmarkComparison);
        Assert.Contains(report.MoneyRisks, risk => risk.Title == "Expensas altas frente al benchmark de la zona");
    }

    [Fact]
    public async Task Report_aggregates_suggested_questions()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        var document = analysis.AddDocument(Guid.NewGuid(), DocumentType.ReservationAgreement, "reserva.pdf", "application/pdf", 1024, "storage/reserva.pdf");
        var finding = new DocumentFinding(
            document.Id,
            analysis.Id,
            FindingSeverity.High,
            FindingCategory.Legal,
            "Reserva no reintegrable",
            "Texto riesgoso.",
            "La reserva se devuelve si aparece un problema documental?");
        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.DocumentFindings.Add(finding);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Contains("La reserva se devuelve si aparece un problema documental?", report.SuggestedQuestions);
    }

    [Fact]
    public async Task Report_includes_disclaimer()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Contains("no reemplaza", report.Disclaimer);
        Assert.Contains("ordenar información", report.Disclaimer);
    }

    [Fact]
    public async Task Report_status_is_ready_with_core_data_and_key_document_analyzed()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        var expenses = analysis.AddDocument(Guid.NewGuid(), DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/expensas.pdf");
        expenses.CompleteAnalysis("Expensas revisadas", LastAnalyzedTextSource.ExtractedText, []);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Equal("Ready", report.ReportStatus);
        Assert.Equal("Informe listo", report.ReportStatusLabel);
    }

    [Fact]
    public async Task Report_status_is_incomplete_without_analyzed_documents()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Equal("Incomplete", report.ReportStatus);
    }

    [Fact]
    public async Task Report_status_needs_review_with_high_finding()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        var expenses = analysis.AddDocument(Guid.NewGuid(), DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/expensas.pdf");
        expenses.CompleteAnalysis("Expensas revisadas", LastAnalyzedTextSource.ExtractedText, []);
        var finding = new DocumentFinding(
            expenses.Id,
            analysis.Id,
            FindingSeverity.High,
            FindingCategory.Financial,
            "Las expensas del documento no coinciden con la publicacion",
            "El valor detectado difiere del publicado.");
        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.DocumentFindings.Add(finding);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Equal("NeedsReview", report.ReportStatus);
        Assert.NotEmpty(report.MoneyRisks);
    }

    [Fact]
    public async Task Conditions_to_advance_include_missing_domain_report()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Contains("Solicitar informe de dominio e inhibiciones.", report.ConditionsToAdvance);
    }

    [Fact]
    public async Task Questions_for_seller_are_aggregated_from_findings()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        var document = analysis.AddDocument(Guid.NewGuid(), DocumentType.ReservationAgreement, "reserva.pdf", "application/pdf", 1024, "storage/reserva.pdf");
        var finding = new DocumentFinding(
            document.Id,
            analysis.Id,
            FindingSeverity.Medium,
            FindingCategory.Legal,
            "Reserva con condicion poco clara",
            "Texto riesgoso.",
            "¿La reserva se devuelve si aparece un problema documental?");
        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.DocumentFindings.Add(finding);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Contains("¿La reserva se devuelve si aparece un problema documental?", report.QuestionsForSellerOrAgent);
    }

    [Fact]
    public async Task Questions_for_notary_are_generated_for_reservation_risk()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        var document = analysis.AddDocument(Guid.NewGuid(), DocumentType.ReservationAgreement, "reserva.pdf", "application/pdf", 1024, "storage/reserva.pdf");
        var finding = new DocumentFinding(
            document.Id,
            analysis.Id,
            FindingSeverity.High,
            FindingCategory.Legal,
            "Reserva no reintegrable",
            "Texto riesgoso.");
        dbContext.PropertyAnalyses.Add(analysis);
        dbContext.DocumentFindings.Add(finding);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Contains("¿Conviene agregar una cláusula de devolución de reserva?", report.QuestionsForNotaryOrLawyer);
    }

    [Fact]
    public async Task Checklist_separates_reviewed_and_pending_documents()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        var expenses = analysis.AddDocument(Guid.NewGuid(), DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/expensas.pdf");
        expenses.CompleteAnalysis("Expensas revisadas", LastAnalyzedTextSource.ExtractedText, []);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        Assert.NotNull(report);
        Assert.Contains(report.ReviewedDocuments, item => item.DocumentLabel == "Expensas" && item.IsAnalyzed);
        Assert.Contains(report.PendingDocuments, item => item.DocumentLabel == "Escritura" && item.IsFuture);
    }

    [Fact]
    public async Task Pdf_generator_creates_pdf_document()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        var expenses = analysis.AddDocument(Guid.NewGuid(), DocumentType.Expenses, "expensas.pdf", "application/pdf", 1024, "storage/expensas.pdf");
        expenses.CompleteAnalysis("Expensas revisadas", LastAnalyzedTextSource.ExtractedText, []);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();
        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        var bytes = new PropertyReportPdfGenerator().Generate(report!);

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 1000);
    }

    [Fact]
    public async Task Pdf_generator_handles_empty_sections()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var analysis = CreateAnalysis(userId);
        dbContext.PropertyAnalyses.Add(analysis);
        await dbContext.SaveChangesAsync();
        var report = await new DeterministicPropertyReportBuilder(dbContext, User(userId)).BuildAsync(analysis.Id);

        var bytes = new PropertyReportPdfGenerator().Generate(report!);

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static PropertyAnalysis CreateAnalysis(
        Guid userId,
        decimal priceUsd = 150000,
        decimal? monthlyExpenses = 90000)
    {
        var analysis = new PropertyAnalysis(
            "Departamento en Palermo",
            "https://example.com",
            "Av. Santa Fe 3200",
            "Palermo",
            priceUsd,
            monthlyExpenses,
            60,
            54,
            3,
            2,
            1,
            12,
            8,
            "B",
            "12",
            true,
            true,
            true,
            PropertyCondition.Good,
            null);

        analysis.AssignOwner(userId);
        analysis.RunInitialAnalysis();
        return analysis;
    }

    private static void MarkChecklistProvided(PropertyAnalysis analysis)
    {
        foreach (var item in analysis.ChecklistItems)
        {
            item.MarkProvided();
        }
    }

    private static TestCurrentUserContext User(Guid userId, bool isAdmin = false)
    {
        return new TestCurrentUserContext(userId, isAdmin);
    }

    private sealed class TestCurrentUserContext(Guid userId, bool isAdmin) : ICurrentUserContext
    {
        public Guid? UserId => userId;
        public string? Email => "test@propsegura.local";
        public string? Role => isAdmin ? "Admin" : "User";
        public bool IsAdmin => isAdmin;
    }
}
