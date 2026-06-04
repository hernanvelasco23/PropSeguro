using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityProtectionScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RiskScore",
                table: "PropertyAnalyses");

            migrationBuilder.AddColumn<int>(
                name: "Bathrooms",
                table: "PropertyAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Bedrooms",
                table: "PropertyAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BenchmarkComparison",
                table: "PropertyAnalyses",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Sin benchmark");

            migrationBuilder.AddColumn<int>(
                name: "BuildingAgeYears",
                table: "PropertyAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CoveredSquareMeters",
                table: "PropertyAnalyses",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Floor",
                table: "PropertyAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAmenities",
                table: "PropertyAnalyses",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBalcony",
                table: "PropertyAnalyses",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitialRiskLevel",
                table: "PropertyAnalyses",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "High");

            migrationBuilder.AddColumn<bool>(
                name: "IsCreditEligible",
                table: "PropertyAnalyses",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpportunityScore",
                table: "PropertyAnalyses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProtectionScore",
                table: "PropertyAnalyses",
                type: "int",
                nullable: false,
                defaultValue: 40);

            migrationBuilder.AddColumn<string>(
                name: "PropertyCondition",
                table: "PropertyAnalyses",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "RecommendedUse",
                table: "PropertyAnalyses",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<int>(
                name: "Rooms",
                table: "PropertyAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalSquareMeters",
                table: "PropertyAnalyses",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UsdPerSquareMeter",
                table: "PropertyAnalyses",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bathrooms",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "Bedrooms",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "BenchmarkComparison",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "BuildingAgeYears",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "CoveredSquareMeters",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "HasAmenities",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "HasBalcony",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "InitialRiskLevel",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "IsCreditEligible",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "OpportunityScore",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "ProtectionScore",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "PropertyCondition",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "RecommendedUse",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "Rooms",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "TotalSquareMeters",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "UsdPerSquareMeter",
                table: "PropertyAnalyses");

            migrationBuilder.AddColumn<int>(
                name: "RiskScore",
                table: "PropertyAnalyses",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
