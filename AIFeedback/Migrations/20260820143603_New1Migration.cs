using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIFeedback.Migrations
{
    /// <inheritdoc />
    public partial class New1Migration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FormatMixedCount",
                table: "AnalysisResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FormatOfflineCount",
                table: "AnalysisResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FormatOnlineCount",
                table: "AnalysisResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormatMixedCount",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "FormatOfflineCount",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "FormatOnlineCount",
                table: "AnalysisResults");
        }
    }
}
