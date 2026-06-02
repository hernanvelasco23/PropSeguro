using Microsoft.EntityFrameworkCore;
using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<PropertyAnalysis> PropertyAnalyses => Set<PropertyAnalysis>();

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
            builder.Property(analysis => analysis.Notes).HasMaxLength(4000);
            builder.Property(analysis => analysis.RiskScore).IsRequired();
            builder.Property(analysis => analysis.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(analysis => analysis.CreatedAtUtc).IsRequired();
            builder.Property(analysis => analysis.UpdatedAtUtc).IsRequired();

            builder.HasMany(analysis => analysis.Documents)
                .WithOne()
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
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(analysis => analysis.ChecklistItems)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PropertyDocument>(builder =>
        {
            builder.ToTable("PropertyDocuments");
            builder.HasKey(document => document.Id);
            builder.Property(document => document.DocumentType).HasConversion<string>().HasMaxLength(80).IsRequired();
            builder.Property(document => document.FileName).HasMaxLength(300).IsRequired();
            builder.Property(document => document.CreatedAtUtc).IsRequired();
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
            builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
            builder.Property(item => item.Category).HasConversion<string>().HasMaxLength(80).IsRequired();
            builder.Property(item => item.IsCompleted).IsRequired();
            builder.Property(item => item.CreatedAtUtc).IsRequired();
        });
    }
}
