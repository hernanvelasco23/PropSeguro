using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PropSeguro.Infrastructure.Persistence;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260602152000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PropertyAnalyses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ListingUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Neighborhood = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                PriceUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                MonthlyExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                RiskScore = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PropertyAnalyses", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "BuyerChecklistItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                PropertyAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BuyerChecklistItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_BuyerChecklistItems_PropertyAnalyses_PropertyAnalysisId",
                    column: x => x.PropertyAnalysisId,
                    principalTable: "PropertyAnalyses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PropertyDocuments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DocumentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                FileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                PropertyAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PropertyDocuments", x => x.Id);
                table.ForeignKey(
                    name: "FK_PropertyDocuments_PropertyAnalyses_PropertyAnalysisId",
                    column: x => x.PropertyAnalysisId,
                    principalTable: "PropertyAnalyses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RiskFindings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Severity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                PropertyAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RiskFindings", x => x.Id);
                table.ForeignKey(
                    name: "FK_RiskFindings_PropertyAnalyses_PropertyAnalysisId",
                    column: x => x.PropertyAnalysisId,
                    principalTable: "PropertyAnalyses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BuyerChecklistItems_PropertyAnalysisId",
            table: "BuyerChecklistItems",
            column: "PropertyAnalysisId");

        migrationBuilder.CreateIndex(
            name: "IX_PropertyDocuments_PropertyAnalysisId",
            table: "PropertyDocuments",
            column: "PropertyAnalysisId");

        migrationBuilder.CreateIndex(
            name: "IX_RiskFindings_PropertyAnalysisId",
            table: "RiskFindings",
            column: "PropertyAnalysisId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "BuyerChecklistItems");
        migrationBuilder.DropTable(name: "PropertyDocuments");
        migrationBuilder.DropTable(name: "RiskFindings");
        migrationBuilder.DropTable(name: "PropertyAnalyses");
    }
}
