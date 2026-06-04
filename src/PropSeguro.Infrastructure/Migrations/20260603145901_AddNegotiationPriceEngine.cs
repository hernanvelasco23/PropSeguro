using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNegotiationPriceEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedFairValueMaxUsd",
                table: "PropertyAnalyses",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedFairValueMinUsd",
                table: "PropertyAnalyses",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketPosition",
                table: "PropertyAnalyses",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NegotiationExplanation",
                table: "PropertyAnalyses",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "NegotiationMarginPercent",
                table: "PropertyAnalyses",
                type: "decimal(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SuggestedFirstOfferUsd",
                table: "PropertyAnalyses",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SuggestedTargetPriceUsd",
                table: "PropertyAnalyses",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedFairValueMaxUsd",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "EstimatedFairValueMinUsd",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "MarketPosition",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "NegotiationExplanation",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "NegotiationMarginPercent",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "SuggestedFirstOfferUsd",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "SuggestedTargetPriceUsd",
                table: "PropertyAnalyses");
        }
    }
}
