using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationTextWorkflowGenerated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EditedText",
                table: "PropertyDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextExtractionStatus",
                table: "PropertyDocuments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<string>(
                name: "SuggestedChange",
                table: "DocumentFindings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedQuestion",
                table: "DocumentFindings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditedText",
                table: "PropertyDocuments");

            migrationBuilder.DropColumn(
                name: "TextExtractionStatus",
                table: "PropertyDocuments");

            migrationBuilder.DropColumn(
                name: "SuggestedChange",
                table: "DocumentFindings");

            migrationBuilder.DropColumn(
                name: "SuggestedQuestion",
                table: "DocumentFindings");
        }
    }
}
