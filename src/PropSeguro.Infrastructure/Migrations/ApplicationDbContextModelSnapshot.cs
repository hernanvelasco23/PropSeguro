using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PropSeguro.Infrastructure.Persistence;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
partial class ApplicationDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.3")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("PropSeguro.Domain.PropertyAnalyses.BuyerChecklistItem", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Category").IsRequired().HasMaxLength(80).HasColumnType("nvarchar(80)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
            b.Property<bool>("IsCompleted").HasColumnType("bit");
            b.Property<Guid?>("PropertyAnalysisId").HasColumnType("uniqueidentifier");
            b.Property<string>("Title").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            b.HasKey("Id");
            b.HasIndex("PropertyAnalysisId");
            b.ToTable("BuyerChecklistItems", (string)null);
        });

        modelBuilder.Entity("PropSeguro.Domain.PropertyAnalyses.PropertyAnalysis", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Address").IsRequired().HasMaxLength(300).HasColumnType("nvarchar(300)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
            b.Property<string>("ListingUrl").HasMaxLength(2048).HasColumnType("nvarchar(2048)");
            b.Property<decimal?>("MonthlyExpenses").HasColumnType("decimal(18,2)");
            b.Property<string>("Neighborhood").IsRequired().HasMaxLength(120).HasColumnType("nvarchar(120)");
            b.Property<string>("Notes").HasMaxLength(4000).HasColumnType("nvarchar(4000)");
            b.Property<decimal?>("PriceUsd").HasColumnType("decimal(18,2)");
            b.Property<int>("RiskScore").HasColumnType("int");
            b.Property<string>("Status").IsRequired().HasMaxLength(40).HasColumnType("nvarchar(40)");
            b.Property<string>("Title").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnType("datetime2");
            b.HasKey("Id");
            b.ToTable("PropertyAnalyses", (string)null);
        });

        modelBuilder.Entity("PropSeguro.Domain.PropertyAnalyses.PropertyDocument", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
            b.Property<string>("DocumentType").IsRequired().HasMaxLength(80).HasColumnType("nvarchar(80)");
            b.Property<string>("FileName").IsRequired().HasMaxLength(300).HasColumnType("nvarchar(300)");
            b.Property<Guid?>("PropertyAnalysisId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("PropertyAnalysisId");
            b.ToTable("PropertyDocuments", (string)null);
        });

        modelBuilder.Entity("PropSeguro.Domain.PropertyAnalyses.RiskFinding", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Code").IsRequired().HasMaxLength(80).HasColumnType("nvarchar(80)");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
            b.Property<string>("Description").IsRequired().HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            b.Property<Guid?>("PropertyAnalysisId").HasColumnType("uniqueidentifier");
            b.Property<string>("Severity").IsRequired().HasMaxLength(40).HasColumnType("nvarchar(40)");
            b.Property<string>("Title").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            b.Property<string>("Type").IsRequired().HasMaxLength(80).HasColumnType("nvarchar(80)");
            b.HasKey("Id");
            b.HasIndex("PropertyAnalysisId");
            b.ToTable("RiskFindings", (string)null);
        });

        modelBuilder.Entity("PropSeguro.Domain.PropertyAnalyses.BuyerChecklistItem", b =>
        {
            b.HasOne("PropSeguro.Domain.PropertyAnalyses.PropertyAnalysis", null)
                .WithMany("ChecklistItems")
                .HasForeignKey("PropertyAnalysisId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity("PropSeguro.Domain.PropertyAnalyses.PropertyDocument", b =>
        {
            b.HasOne("PropSeguro.Domain.PropertyAnalyses.PropertyAnalysis", null)
                .WithMany("Documents")
                .HasForeignKey("PropertyAnalysisId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity("PropSeguro.Domain.PropertyAnalyses.RiskFinding", b =>
        {
            b.HasOne("PropSeguro.Domain.PropertyAnalyses.PropertyAnalysis", null)
                .WithMany("RiskFindings")
                .HasForeignKey("PropertyAnalysisId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity("PropSeguro.Domain.PropertyAnalyses.PropertyAnalysis", b =>
        {
            b.Navigation("ChecklistItems");
            b.Navigation("Documents");
            b.Navigation("RiskFindings");
        });
    }
}
