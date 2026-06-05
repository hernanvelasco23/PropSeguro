using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<PropertyAnalysis> PropertyAnalyses => Set<PropertyAnalysis>();
    public DbSet<PropertyDocument> PropertyDocuments => Set<PropertyDocument>();
    public DbSet<BuyerChecklistItem> BuyerChecklistItems => Set<BuyerChecklistItem>();
    public DbSet<DocumentFinding> DocumentFindings => Set<DocumentFinding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PropertyAnalysis>(builder =>
        {
            builder.ToTable("PropertyAnalyses");
            builder.HasKey(analysis => analysis.Id);

            builder.Property(analysis => analysis.Title).HasMaxLength(200).IsRequired();
            builder.Property(analysis => analysis.ListingUrl).HasMaxLength(2048);
            builder.Property(analysis => analysis.Address).HasMaxLength(300).IsRequired();
            builder.Property(analysis => analysis.Neighborhood).HasMaxLength(120).IsRequired();
            builder.Property(analysis => analysis.PriceUsd).HasColumnType("decimal(18,2)");
            builder.Property(analysis => analysis.MonthlyExpenses).HasColumnType("decimal(18,2)");
            builder.Property(analysis => analysis.TotalSquareMeters).HasColumnType("decimal(10,2)");
            builder.Property(analysis => analysis.CoveredSquareMeters).HasColumnType("decimal(10,2)");
            builder.Property(analysis => analysis.Rooms);
            builder.Property(analysis => analysis.Bedrooms);
            builder.Property(analysis => analysis.Bathrooms);
            builder.Property(analysis => analysis.BuildingAgeYears);
            builder.Property(analysis => analysis.Floor);
            builder.Property(analysis => analysis.HasBalcony);
            builder.Property(analysis => analysis.HasAmenities);
            builder.Property(analysis => analysis.IsCreditEligible);
            builder.Property(analysis => analysis.PropertyCondition).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(analysis => analysis.Notes).HasMaxLength(4000);
            builder.Property(analysis => analysis.OpportunityScore).IsRequired();
            builder.Property(analysis => analysis.ProtectionScore).IsRequired();
            builder.Property(analysis => analysis.InitialRiskLevel).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(analysis => analysis.RecommendedUse).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(analysis => analysis.UsdPerSquareMeter).HasColumnType("decimal(10,2)");
            builder.Property(analysis => analysis.BenchmarkComparison).HasMaxLength(80).IsRequired();
            builder.Property(analysis => analysis.MarketPosition).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(analysis => analysis.EstimatedFairValueMinUsd).HasColumnType("decimal(18,2)");
            builder.Property(analysis => analysis.EstimatedFairValueMaxUsd).HasColumnType("decimal(18,2)");
            builder.Property(analysis => analysis.SuggestedFirstOfferUsd).HasColumnType("decimal(18,2)");
            builder.Property(analysis => analysis.SuggestedTargetPriceUsd).HasColumnType("decimal(18,2)");
            builder.Property(analysis => analysis.NegotiationMarginPercent).HasColumnType("decimal(6,2)");
            builder.Property(analysis => analysis.NegotiationExplanation).HasMaxLength(1000).IsRequired();
            builder.Property(analysis => analysis.CoverImageFileName).HasMaxLength(300);
            builder.Property(analysis => analysis.CoverImageContentType).HasMaxLength(150);
            builder.Property(analysis => analysis.CoverImageSizeBytes);
            builder.Property(analysis => analysis.CoverImageStoragePath).HasMaxLength(1000);
            builder.Property(analysis => analysis.CoverImageUploadedAtUtc);
            builder.Property(analysis => analysis.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(analysis => analysis.CreatedAtUtc).IsRequired();
            builder.Property(analysis => analysis.UpdatedAtUtc).IsRequired();

            builder.HasMany(analysis => analysis.Documents)
                .WithOne()
                .HasForeignKey(document => document.PropertyAnalysisId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(analysis => analysis.Documents)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(analysis => analysis.RiskFindings)
                .WithOne()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(analysis => analysis.RiskFindings)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(analysis => analysis.ChecklistItems)
                .WithOne()
                .HasForeignKey(item => item.PropertyAnalysisId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(analysis => analysis.ChecklistItems)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PropertyDocument>(builder =>
        {
            builder.ToTable("PropertyDocuments");
            builder.HasKey(document => document.Id);
            builder.Property(document => document.PropertyAnalysisId).IsRequired();
            builder.Property(document => document.DocumentType).HasConversion<string>().HasMaxLength(80).IsRequired();
            builder.Property(document => document.FileName).HasMaxLength(300).IsRequired();
            builder.Property(document => document.ContentType).HasMaxLength(150).IsRequired();
            builder.Property(document => document.SizeBytes).IsRequired();
            builder.Property(document => document.StoragePath).HasMaxLength(1000).IsRequired();
            builder.Property(document => document.UploadedAtUtc).IsRequired();
            builder.Property(document => document.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(document => document.ExtractedText);
            builder.Property(document => document.EditedText);
            builder.Property(document => document.TextExtractionStatus).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(document => document.TextExtractionQuality).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(document => document.LastAnalyzedTextSource).HasConversion<string>().HasMaxLength(40);
            builder.Property(document => document.AnalysisStatus).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(document => document.AnalyzedAtUtc);
            builder.Property(document => document.AnalysisSummary).HasMaxLength(1000);
        });

        modelBuilder.Entity<DocumentFinding>(builder =>
        {
            builder.ToTable("DocumentFindings");
            builder.HasKey(finding => finding.Id);
            builder.Property(finding => finding.PropertyDocumentId).IsRequired();
            builder.Property(finding => finding.PropertyAnalysisId).IsRequired();
            builder.Property(finding => finding.Severity).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(finding => finding.Category).HasConversion<string>().HasMaxLength(80).IsRequired();
            builder.Property(finding => finding.Title).HasMaxLength(200).IsRequired();
            builder.Property(finding => finding.Description).HasMaxLength(1000).IsRequired();
            builder.Property(finding => finding.BuyerImpact).HasMaxLength(1000);
            builder.Property(finding => finding.SuggestedQuestion).HasMaxLength(1000);
            builder.Property(finding => finding.SuggestedChange).HasMaxLength(1000);
            builder.Property(finding => finding.SuggestedNegotiationPoint).HasMaxLength(1000);
            builder.Property(finding => finding.MatchedText).HasMaxLength(500);
            builder.Property(finding => finding.CreatedAtUtc).IsRequired();

            builder.HasIndex(finding => finding.PropertyAnalysisId);
            builder.HasIndex(finding => finding.PropertyDocumentId);

            builder.HasOne<PropertyAnalysis>()
                .WithMany()
                .HasForeignKey(finding => finding.PropertyAnalysisId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<PropertyDocument>()
                .WithMany()
                .HasForeignKey(finding => finding.PropertyDocumentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RiskFinding>(builder =>
        {
            builder.ToTable("RiskFindings");
            builder.HasKey(finding => finding.Id);
            builder.Property(finding => finding.Type).HasConversion<string>().HasMaxLength(80).IsRequired();
            builder.Property(finding => finding.Severity).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(finding => finding.Code).HasMaxLength(80).IsRequired();
            builder.Property(finding => finding.Title).HasMaxLength(200).IsRequired();
            builder.Property(finding => finding.Description).HasMaxLength(1000).IsRequired();
            builder.Property(finding => finding.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<BuyerChecklistItem>(builder =>
        {
            builder.ToTable("BuyerChecklistItems");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.PropertyAnalysisId).IsRequired();
            builder.Property(item => item.DocumentType).HasConversion<string>().HasMaxLength(80).IsRequired();
            builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
            builder.Property(item => item.Description).HasMaxLength(1000).IsRequired();
            builder.Property(item => item.IsRequired).IsRequired();
            builder.Property(item => item.IsProvided).IsRequired();
            builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.CreatedAtUtc).IsRequired();
        });
    }
}
