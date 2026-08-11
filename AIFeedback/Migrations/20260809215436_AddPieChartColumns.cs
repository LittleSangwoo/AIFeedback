using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIFeedback.Migrations
{
    /// <inheritdoc />
    public partial class AddPieChartColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Dist1to3",
                table: "AnalysisResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Dist4to7",
                table: "AnalysisResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Dist8to10",
                table: "AnalysisResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Dist1to3",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "Dist4to7",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "Dist8to10",
                table: "AnalysisResults");
        }
    }
}
