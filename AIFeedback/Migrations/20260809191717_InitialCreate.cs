using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIFeedback.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisResults",
                columns: table => new
                {
                    SessionName = table.Column<string>(type: "TEXT", nullable: false),
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProgramName = table.Column<string>(type: "TEXT", nullable: false),
                    ListenerCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UsefulnessAvg = table.Column<double>(type: "REAL", nullable: false),
                    AvailabilityAvg = table.Column<double>(type: "REAL", nullable: false),
                    PracticalityAvg = table.Column<double>(type: "REAL", nullable: false),
                    InteractionAvg = table.Column<double>(type: "REAL", nullable: false),
                    EngagementYesPercent = table.Column<double>(type: "REAL", nullable: false),
                    OverallSatisfaction = table.Column<double>(type: "REAL", nullable: false),
                    ThemesJson = table.Column<string>(type: "TEXT", nullable: false),
                    SentimentJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProblemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    QuotesJson = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendationsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RawComments = table.Column<string>(type: "TEXT", nullable: true),
                    DateProcessed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AvgUtility = table.Column<double>(type: "REAL", nullable: false),
                    AvgPractice = table.Column<double>(type: "REAL", nullable: false),
                    AvgAccessibility = table.Column<double>(type: "REAL", nullable: false),
                    AvgEngagement = table.Column<double>(type: "REAL", nullable: false),
                    AiInsightsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessingTimeMs = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisResults", x => x.SessionName);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisResults");
        }
    }
}
