using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameBug.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2HardeningUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "model_name",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "model_provider",
                table: "analysis_runs");

            migrationBuilder.RenameColumn(
                name: "prompt_version",
                table: "analysis_runs",
                newName: "routing_policy_version");

            migrationBuilder.AddColumn<Guid>(
                name: "selected_repro_execution_id",
                table: "analysis_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "analysis_ai_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    route_profile = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    routing_reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requested_model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    resolved_model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    schema_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    routing_policy_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    safe_error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    latency_ms = table.Column<long>(type: "bigint", nullable: true),
                    input_tokens = table.Column<int>(type: "integer", nullable: true),
                    output_tokens = table.Column<int>(type: "integer", nullable: true),
                    provider_request_id_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    output_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    is_selected = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_ai_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_analysis_ai_executions_analysis_runs_analysis_run_id",
                        column: x => x.analysis_run_id,
                        principalTable: "analysis_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_runs_selected_repro_execution_id",
                table: "analysis_runs",
                column: "selected_repro_execution_id");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_ai_executions_analysis_run_id",
                table: "analysis_ai_executions",
                column: "analysis_run_id");

            migrationBuilder.AddForeignKey(
                name: "FK_analysis_runs_analysis_ai_executions_selected_repro_executi~",
                table: "analysis_runs",
                column: "selected_repro_execution_id",
                principalTable: "analysis_ai_executions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_analysis_runs_analysis_ai_executions_selected_repro_executi~",
                table: "analysis_runs");

            migrationBuilder.DropTable(
                name: "analysis_ai_executions");

            migrationBuilder.DropIndex(
                name: "IX_analysis_runs_selected_repro_execution_id",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "selected_repro_execution_id",
                table: "analysis_runs");

            migrationBuilder.RenameColumn(
                name: "routing_policy_version",
                table: "analysis_runs",
                newName: "prompt_version");

            migrationBuilder.AddColumn<string>(
                name: "model_name",
                table: "analysis_runs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model_provider",
                table: "analysis_runs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
