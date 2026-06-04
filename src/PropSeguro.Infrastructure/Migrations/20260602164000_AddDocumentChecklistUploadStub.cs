using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PropSeguro.Infrastructure.Persistence;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260602164000_AddDocumentChecklistUploadStub")]
public partial class AddDocumentChecklistUploadStub : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BuyerChecklistItems_PropertyAnalyses_PropertyAnalysisId",
            table: "BuyerChecklistItems");

        migrationBuilder.DropForeignKey(
            name: "FK_PropertyDocuments_PropertyAnalyses_PropertyAnalysisId",
            table: "PropertyDocuments");

        migrationBuilder.DropIndex(
            name: "IX_BuyerChecklistItems_PropertyAnalysisId",
            table: "BuyerChecklistItems");

        migrationBuilder.DropIndex(
            name: "IX_PropertyDocuments_PropertyAnalysisId",
            table: "PropertyDocuments");

        migrationBuilder.Sql("DELETE FROM BuyerChecklistItems WHERE PropertyAnalysisId IS NULL");
        migrationBuilder.Sql("DELETE FROM PropertyDocuments WHERE PropertyAnalysisId IS NULL");

        migrationBuilder.DropColumn(
            name: "Category",
            table: "BuyerChecklistItems");

        migrationBuilder.DropColumn(
            name: "IsCompleted",
            table: "BuyerChecklistItems");

        migrationBuilder.RenameColumn(
            name: "CreatedAtUtc",
            table: "PropertyDocuments",
            newName: "UploadedAtUtc");

        migrationBuilder.AlterColumn<Guid>(
            name: "PropertyAnalysisId",
            table: "PropertyDocuments",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ContentType",
            table: "PropertyDocuments",
            type: "nvarchar(150)",
            maxLength: 150,
            nullable: false,
            defaultValue: "application/octet-stream");

        migrationBuilder.AddColumn<long>(
            name: "SizeBytes",
            table: "PropertyDocuments",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "PropertyDocuments",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "Uploaded");

        migrationBuilder.AlterColumn<Guid>(
            name: "PropertyAnalysisId",
            table: "BuyerChecklistItems",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "BuyerChecklistItems",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "DocumentType",
            table: "BuyerChecklistItems",
            type: "nvarchar(80)",
            maxLength: 80,
            nullable: false,
            defaultValue: "Other");

        migrationBuilder.AddColumn<bool>(
            name: "IsProvided",
            table: "BuyerChecklistItems",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsRequired",
            table: "BuyerChecklistItems",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "BuyerChecklistItems",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "Missing");

        migrationBuilder.CreateIndex(
            name: "IX_BuyerChecklistItems_PropertyAnalysisId",
            table: "BuyerChecklistItems",
            column: "PropertyAnalysisId");

        migrationBuilder.CreateIndex(
            name: "IX_PropertyDocuments_PropertyAnalysisId",
            table: "PropertyDocuments",
            column: "PropertyAnalysisId");

        migrationBuilder.AddForeignKey(
            name: "FK_BuyerChecklistItems_PropertyAnalyses_PropertyAnalysisId",
            table: "BuyerChecklistItems",
            column: "PropertyAnalysisId",
            principalTable: "PropertyAnalyses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_PropertyDocuments_PropertyAnalyses_PropertyAnalysisId",
            table: "PropertyDocuments",
            column: "PropertyAnalysisId",
            principalTable: "PropertyAnalyses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BuyerChecklistItems_PropertyAnalyses_PropertyAnalysisId",
            table: "BuyerChecklistItems");

        migrationBuilder.DropForeignKey(
            name: "FK_PropertyDocuments_PropertyAnalyses_PropertyAnalysisId",
            table: "PropertyDocuments");

        migrationBuilder.DropIndex(
            name: "IX_BuyerChecklistItems_PropertyAnalysisId",
            table: "BuyerChecklistItems");

        migrationBuilder.DropIndex(
            name: "IX_PropertyDocuments_PropertyAnalysisId",
            table: "PropertyDocuments");

        migrationBuilder.DropColumn(name: "ContentType", table: "PropertyDocuments");
        migrationBuilder.DropColumn(name: "SizeBytes", table: "PropertyDocuments");
        migrationBuilder.DropColumn(name: "Status", table: "PropertyDocuments");
        migrationBuilder.DropColumn(name: "Description", table: "BuyerChecklistItems");
        migrationBuilder.DropColumn(name: "DocumentType", table: "BuyerChecklistItems");
        migrationBuilder.DropColumn(name: "IsProvided", table: "BuyerChecklistItems");
        migrationBuilder.DropColumn(name: "IsRequired", table: "BuyerChecklistItems");
        migrationBuilder.DropColumn(name: "Status", table: "BuyerChecklistItems");

        migrationBuilder.RenameColumn(
            name: "UploadedAtUtc",
            table: "PropertyDocuments",
            newName: "CreatedAtUtc");

        migrationBuilder.AlterColumn<Guid>(
            name: "PropertyAnalysisId",
            table: "PropertyDocuments",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AlterColumn<Guid>(
            name: "PropertyAnalysisId",
            table: "BuyerChecklistItems",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AddColumn<string>(
            name: "Category",
            table: "BuyerChecklistItems",
            type: "nvarchar(80)",
            maxLength: 80,
            nullable: false,
            defaultValue: "Documentation");

        migrationBuilder.AddColumn<bool>(
            name: "IsCompleted",
            table: "BuyerChecklistItems",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_BuyerChecklistItems_PropertyAnalysisId",
            table: "BuyerChecklistItems",
            column: "PropertyAnalysisId");

        migrationBuilder.CreateIndex(
            name: "IX_PropertyDocuments_PropertyAnalysisId",
            table: "PropertyDocuments",
            column: "PropertyAnalysisId");

        migrationBuilder.AddForeignKey(
            name: "FK_BuyerChecklistItems_PropertyAnalyses_PropertyAnalysisId",
            table: "BuyerChecklistItems",
            column: "PropertyAnalysisId",
            principalTable: "PropertyAnalyses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_PropertyDocuments_PropertyAnalyses_PropertyAnalysisId",
            table: "PropertyDocuments",
            column: "PropertyAnalysisId",
            principalTable: "PropertyAnalyses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
