using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameBug.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Hardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "result_reference",
                table: "analysis_runs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_repro_cases_confidence",
                table: "repro_cases",
                sql: "confidence >= 0 AND confidence <= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_evidence_facts_confidence",
                table: "evidence_facts",
                sql: "confidence >= 0 AND confidence <= 1");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_runs_report_id_configuration_hash",
                table: "analysis_runs",
                columns: new[] { "report_id", "configuration_hash" },
                unique: true,
                filter: "status IN ('Received', 'Queued', 'Processing')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_analysis_runs_version",
                table: "analysis_runs",
                sql: "version > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_repro_cases_confidence",
                table: "repro_cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_evidence_facts_confidence",
                table: "evidence_facts");

            migrationBuilder.DropIndex(
                name: "IX_analysis_runs_report_id_configuration_hash",
                table: "analysis_runs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_analysis_runs_version",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "result_reference",
                table: "analysis_runs");
        }
    }
}
