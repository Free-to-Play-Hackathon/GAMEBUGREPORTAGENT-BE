using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameBug.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8EvaluationMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluation_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifest_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    manifest_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    configuration_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    protocol_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    dataset_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ground_truth_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    schema_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    sanitizer_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    parser_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    routing_policy_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    embedding_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ranker_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    trust_policy_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    source_commit = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    build_version = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    validity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    invalid_reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    metrics_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "worker_heartbeats",
                columns: table => new
                {
                    worker_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    last_heartbeat_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_heartbeats", x => x.worker_name);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_case_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    expected_duplicate_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    actual_top_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    actual_rank = table.Column<int>(type: "integer", nullable: true),
                    actual_classification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    latency_ms = table.Column<long>(type: "bigint", nullable: true),
                    error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_case_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluation_case_results_evaluation_runs_evaluation_run_id",
                        column: x => x.evaluation_run_id,
                        principalTable: "evaluation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_case_results_evaluation_run_id_case_id",
                table: "evaluation_case_results",
                columns: new[] { "evaluation_run_id", "case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_runs_manifest_hash_configuration_hash",
                table: "evaluation_runs",
                columns: new[] { "manifest_hash", "configuration_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_runs_status_created_at",
                table: "evaluation_runs",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_case_results");

            migrationBuilder.DropTable(
                name: "worker_heartbeats");

            migrationBuilder.DropTable(
                name: "evaluation_runs");
        }
    }
}
