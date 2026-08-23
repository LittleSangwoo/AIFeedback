using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIFeedback.Migrations
{
    /// <inheritdoc />
    public partial class New4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AvailabilityMedian",
                table: "AnalysisResults",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "AvailabilityStdDev",
                table: "AnalysisResults",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "DuplicateRowsRemoved",
                table: "AnalysisResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "InteractionMedian",
                table: "AnalysisResults",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "InteractionStdDev",
                table: "AnalysisResults",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PracticalityMedian",
                table: "AnalysisResults",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PracticalityStdDev",
                table: "AnalysisResults",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "UsefulnessMedian",
                table: "AnalysisResults",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "UsefulnessStdDev",
                table: "AnalysisResults",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailabilityMedian",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "AvailabilityStdDev",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "DuplicateRowsRemoved",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "InteractionMedian",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "InteractionStdDev",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "PracticalityMedian",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "PracticalityStdDev",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "UsefulnessMedian",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "UsefulnessStdDev",
                table: "AnalysisResults");
        }
    }
}
