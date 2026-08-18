using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIFeedback.Migrations
{
    /// <inheritdoc />
    public partial class NewMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScoresDistributionJson",
                table: "AnalysisResults",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScoresDistributionJson",
                table: "AnalysisResults");
        }
    }
}
