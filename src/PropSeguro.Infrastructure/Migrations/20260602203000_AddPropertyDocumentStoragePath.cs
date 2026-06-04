using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyDocumentStoragePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "PropertyDocuments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: string.Empty);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "PropertyDocuments");
        }
    }
}
