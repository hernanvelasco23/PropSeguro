using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations
{
    public partial class AddPropertyUnitFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApartmentUnit",
                table: "PropertyAnalyses",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitIdentifier",
                table: "PropertyAnalyses",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApartmentUnit",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "UnitIdentifier",
                table: "PropertyAnalyses");
        }
    }
}
