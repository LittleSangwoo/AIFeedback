using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIFeedback.Migrations
{
    /// <inheritdoc />
    public partial class ThiredMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationMatrixJson",
                table: "AnalysisResults",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrelationMatrixJson",
                table: "AnalysisResults");
        }
    }
}
