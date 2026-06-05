using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyCoverImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverImageContentType",
                table: "PropertyAnalyses",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageFileName",
                table: "PropertyAnalyses",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CoverImageSizeBytes",
                table: "PropertyAnalyses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageStoragePath",
                table: "PropertyAnalyses",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoverImageUploadedAtUtc",
                table: "PropertyAnalyses",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImageContentType",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "CoverImageFileName",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "CoverImageSizeBytes",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "CoverImageStoragePath",
                table: "PropertyAnalyses");

            migrationBuilder.DropColumn(
                name: "CoverImageUploadedAtUtc",
                table: "PropertyAnalyses");
        }
    }
}
