using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentMatchConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<System.DateTime>(
                name: "UserConfirmedMatchesPropertyAtUtc",
                table: "PropertyDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UserConfirmedMatchesProperty",
                table: "PropertyDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserConfirmedMatchesPropertyAtUtc",
                table: "PropertyDocuments");

            migrationBuilder.DropColumn(
                name: "UserConfirmedMatchesProperty",
                table: "PropertyDocuments");
        }
    }
}
