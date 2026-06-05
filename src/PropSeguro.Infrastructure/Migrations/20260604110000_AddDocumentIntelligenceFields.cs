using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentIntelligenceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuyerImpact",
                table: "DocumentFindings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchedText",
                table: "DocumentFindings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedNegotiationPoint",
                table: "DocumentFindings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAnalyzedTextSource",
                table: "PropertyDocuments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextExtractionQuality",
                table: "PropertyDocuments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Unknown");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyerImpact",
                table: "DocumentFindings");

            migrationBuilder.DropColumn(
                name: "MatchedText",
                table: "DocumentFindings");

            migrationBuilder.DropColumn(
                name: "SuggestedNegotiationPoint",
                table: "DocumentFindings");

            migrationBuilder.DropColumn(
                name: "LastAnalyzedTextSource",
                table: "PropertyDocuments");

            migrationBuilder.DropColumn(
                name: "TextExtractionQuality",
                table: "PropertyDocuments");
        }
    }
}
