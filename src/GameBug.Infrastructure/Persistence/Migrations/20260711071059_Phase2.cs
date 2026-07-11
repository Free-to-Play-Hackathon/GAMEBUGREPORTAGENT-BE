using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameBug.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analysis_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    input_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    configuration_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    schema_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sanitizer_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    parser_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    prompt_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    model_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    model_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    version_token = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    warnings_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expected_behaviors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger = table.Column<string>(type: "text", nullable: false),
                    expected_outcome = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    build_range_start = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    build_range_end = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expected_behaviors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_entities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    canonical_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    aliases = table.Column<string[]>(type: "text[]", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    build_range_start = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    build_range_end = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_entities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_timeline",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    relative_sequence = table.Column<int>(type: "integer", nullable: false),
                    event_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    excerpt = table.Column<string>(type: "text", nullable: false),
                    excerpt_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_ref = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    source_line = table.Column<int>(type: "integer", nullable: true),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_timeline", x => x.id);
                    table.ForeignKey(
                        name: "FK_event_timeline_analysis_runs_analysis_run_id",
                        column: x => x.analysis_run_id,
                        principalTable: "analysis_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evidence_facts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fact_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    normalized_value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_facts", x => x.id);
                    table.ForeignKey(
                        name: "FK_evidence_facts_analysis_runs_analysis_run_id",
                        column: x => x.analysis_run_id,
                        principalTable: "analysis_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repro_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    build_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    platform = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    preconditions = table.Column<string>(type: "text", nullable: false),
                    expected_result = table.Column<string>(type: "text", nullable: false),
                    actual_result = table.Column<string>(type: "text", nullable: false),
                    severity_estimate = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    severity_reason = table.Column<string>(type: "text", nullable: false),
                    missing_information = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repro_cases", x => x.id);
                    table.ForeignKey(
                        name: "FK_repro_cases_analysis_runs_analysis_run_id",
                        column: x => x.analysis_run_id,
                        principalTable: "analysis_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evidence_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_ref = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    line_start = table.Column<int>(type: "integer", nullable: true),
                    line_end = table.Column<int>(type: "integer", nullable: true),
                    sanitized_excerpt = table.Column<string>(type: "text", nullable: false),
                    excerpt_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    trust_level = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    evidence_fact_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_sources", x => x.id);
                    table.ForeignKey(
                        name: "FK_evidence_sources_evidence_facts_evidence_fact_id",
                        column: x => x.evidence_fact_id,
                        principalTable: "evidence_facts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repro_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    step_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inference_reason = table.Column<string>(type: "text", nullable: true),
                    repro_case_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repro_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_repro_steps_repro_cases_repro_case_id",
                        column: x => x.repro_case_id,
                        principalTable: "repro_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_runs_report_id_version",
                table: "analysis_runs",
                columns: new[] { "report_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_analysis_runs_status_started_at",
                table: "analysis_runs",
                columns: new[] { "status", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_event_timeline_analysis_run_id",
                table: "event_timeline",
                column: "analysis_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_facts_analysis_run_id",
                table: "evidence_facts",
                column: "analysis_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_sources_evidence_fact_id",
                table: "evidence_sources",
                column: "evidence_fact_id");

            migrationBuilder.CreateIndex(
                name: "IX_game_entities_canonical_name",
                table: "game_entities",
                column: "canonical_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repro_cases_analysis_run_id",
                table: "repro_cases",
                column: "analysis_run_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repro_steps_repro_case_id_step_order",
                table: "repro_steps",
                columns: new[] { "repro_case_id", "step_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_timeline");

            migrationBuilder.DropTable(
                name: "evidence_sources");

            migrationBuilder.DropTable(
                name: "expected_behaviors");

            migrationBuilder.DropTable(
                name: "game_entities");

            migrationBuilder.DropTable(
                name: "repro_steps");

            migrationBuilder.DropTable(
                name: "evidence_facts");

            migrationBuilder.DropTable(
                name: "repro_cases");

            migrationBuilder.DropTable(
                name: "analysis_runs");
        }
    }
}
