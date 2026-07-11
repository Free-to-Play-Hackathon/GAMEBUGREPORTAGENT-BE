using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameBug.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2ContractAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_analysis_runs_report_id_configuration_hash",
                table: "analysis_runs");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_runs_report_id_input_hash_configuration_hash",
                table: "analysis_runs",
                columns: new[] { "report_id", "input_hash", "configuration_hash" },
                unique: true,
                filter: "status IN ('Received', 'Queued', 'Processing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_analysis_runs_report_id_input_hash_configuration_hash",
                table: "analysis_runs");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_runs_report_id_configuration_hash",
                table: "analysis_runs",
                columns: new[] { "report_id", "configuration_hash" },
                unique: true,
                filter: "status IN ('Received', 'Queued', 'Processing')");
        }
    }
}
