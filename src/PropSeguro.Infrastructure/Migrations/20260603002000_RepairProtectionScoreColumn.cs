using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PropSeguro.Infrastructure.Persistence;

#nullable disable

namespace PropSeguro.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260603002000_RepairProtectionScoreColumn")]
    public partial class RepairProtectionScoreColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('PropertyAnalyses', 'ProtectionScore') IS NULL
                BEGIN
                    IF COL_LENGTH('PropertyAnalyses', 'RiskScore') IS NOT NULL
                    BEGIN
                        EXEC sp_rename 'PropertyAnalyses.RiskScore', 'ProtectionScore', 'COLUMN';
                    END
                    ELSE
                    BEGIN
                        ALTER TABLE PropertyAnalyses ADD ProtectionScore int NOT NULL CONSTRAINT DF_PropertyAnalyses_ProtectionScore DEFAULT 40;
                    END
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('PropertyAnalyses', 'RiskScore') IS NULL
                    AND COL_LENGTH('PropertyAnalyses', 'ProtectionScore') IS NOT NULL
                BEGIN
                    EXEC sp_rename 'PropertyAnalyses.ProtectionScore', 'RiskScore', 'COLUMN';
                END
                """);
        }
    }
}
