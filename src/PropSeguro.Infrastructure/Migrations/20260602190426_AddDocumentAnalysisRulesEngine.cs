using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAnalysisRulesEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalysisStatus",
                table: "PropertyDocuments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<string>(
                name: "AnalysisSummary",
                table: "PropertyDocuments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnalyzedAtUtc",
                table: "PropertyDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "PropertyDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentFindings_PropertyAnalyses_PropertyAnalysisId",
                        column: x => x.PropertyAnalysisId,
                        principalTable: "PropertyAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentFindings_PropertyDocuments_PropertyDocumentId",
                        column: x => x.PropertyDocumentId,
                        principalTable: "PropertyDocuments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFindings_PropertyAnalysisId",
                table: "DocumentFindings",
                column: "PropertyAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFindings_PropertyDocumentId",
                table: "DocumentFindings",
                column: "PropertyDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentFindings");

            migrationBuilder.DropColumn(
                name: "AnalysisStatus",
                table: "PropertyDocuments");

            migrationBuilder.DropColumn(
                name: "AnalysisSummary",
                table: "PropertyDocuments");

            migrationBuilder.DropColumn(
                name: "AnalyzedAtUtc",
                table: "PropertyDocuments");

            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "PropertyDocuments");
        }
    }
}
