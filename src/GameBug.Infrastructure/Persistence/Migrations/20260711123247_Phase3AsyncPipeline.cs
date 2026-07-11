using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameBug.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3AsyncPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancellation_requested_at",
                table: "analysis_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "current_attempt",
                table: "analysis_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "failure_category",
                table: "analysis_runs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_heartbeat_at",
                table: "analysis_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_retry_at",
                table: "analysis_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "progress_percent",
                table: "analysis_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "queued_at",
                table: "analysis_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                table: "analysis_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "analysis_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "analysis_checkpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    stage_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    input_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    output_reference = table.Column<string>(type: "jsonb", nullable: true),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    warning_codes = table.Column<string>(type: "jsonb", nullable: true),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_checkpoints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "analysis_execution_locks",
                columns: table => new
                {
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locked_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_execution_locks", x => x.analysis_run_id);
                });

            migrationBuilder.CreateTable(
                name: "analysis_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    queue_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expected_version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_jobs", x => x.id);
                    table.CheckConstraint("CK_analysis_jobs_attempt", "attempt_count >= 0");
                });

            migrationBuilder.CreateTable(
                name: "analysis_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dispatch_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_outbox", x => x.id);
                    table.CheckConstraint("CK_analysis_outbox_attempt", "attempt_count >= 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_analysis_runs_progress",
                table: "analysis_runs",
                sql: "progress_percent >= 0 AND progress_percent <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_analysis_runs_retry_count",
                table: "analysis_runs",
                sql: "retry_count >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_attempts_analysis_run_id_attempt_number",
                table: "analysis_attempts",
                columns: new[] { "analysis_run_id", "attempt_number" });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_checkpoints_analysis_run_id_stage_stage_version_in~",
                table: "analysis_checkpoints",
                columns: new[] { "analysis_run_id", "stage", "stage_version", "input_hash" },
                unique: true,
                filter: "status = 'Completed'");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_jobs_analysis_run_id",
                table: "analysis_jobs",
                column: "analysis_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_jobs_queue_name_status_available_at",
                table: "analysis_jobs",
                columns: new[] { "queue_name", "status", "available_at" });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_outbox_aggregate_id",
                table: "analysis_outbox",
                column: "aggregate_id");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_outbox_dispatch_status_next_attempt_at",
                table: "analysis_outbox",
                columns: new[] { "dispatch_status", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_attempts");

            migrationBuilder.DropTable(
                name: "analysis_checkpoints");

            migrationBuilder.DropTable(
                name: "analysis_execution_locks");

            migrationBuilder.DropTable(
                name: "analysis_jobs");

            migrationBuilder.DropTable(
                name: "analysis_outbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_analysis_runs_progress",
                table: "analysis_runs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_analysis_runs_retry_count",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "cancellation_requested_at",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "current_attempt",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "failure_category",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "last_heartbeat_at",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "next_retry_at",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "progress_percent",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "queued_at",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "retry_count",
                table: "analysis_runs");
        }
    }
}
