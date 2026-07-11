using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace GameBug.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4PgvectorUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "duplicate_matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    historical_ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    final_score = table.Column<double>(type: "double precision", nullable: false),
                    classification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    channel_scores_json = table.Column<string>(type: "jsonb", nullable: false),
                    signal_scores_json = table.Column<string>(type: "jsonb", nullable: false),
                    matching_signals_json = table.Column<string>(type: "jsonb", nullable: false),
                    conflicting_signals_json = table.Column<string>(type: "jsonb", nullable: false),
                    explanation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ranker_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    reranker_model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    reranker_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    candidate_snapshot_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_duplicate_matches", x => x.id);
                    table.CheckConstraint("CK_duplicate_matches_rank", "rank > 0");
                    table.CheckConstraint("CK_duplicate_matches_score", "final_score >= 0 AND final_score <= 1");
                });

            migrationBuilder.CreateTable(
                name: "embedding_cache",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    embedding_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    vector = table.Column<Vector>(type: "vector", nullable: false),
                    dimension = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_embedding_cache", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "historical_tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    summary_sanitized = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    severity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    build_min = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    build_max = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    platforms = table.Column<string[]>(type: "text[]", nullable: false),
                    stack_signature = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    stack_summary = table.Column<string>(type: "text", nullable: true),
                    game_entities = table.Column<string[]>(type: "text[]", nullable: false),
                    symptom = table.Column<string>(type: "text", nullable: true),
                    trigger_action = table.Column<string>(type: "text", nullable: true),
                    scene_or_feature = table.Column<string>(type: "text", nullable: true),
                    actual_result = table.Column<string>(type: "text", nullable: true),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    search_text_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    embedding = table.Column<Vector>(type: "vector", nullable: true),
                    embedding_provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    embedding_model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    embedding_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    embedding_dimension = table.Column<int>(type: "integer", nullable: true),
                    source_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    indexed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    import_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historical_tickets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_import_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    accepted_count = table.Column<int>(type: "integer", nullable: false),
                    rejected_count = table.Column<int>(type: "integer", nullable: false),
                    import_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    actor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    errors_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_import_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "historical_ticket_index_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_historical_ticket_index_jobs", x => x.id);
                    table.CheckConstraint("CK_historical_ticket_index_jobs_attempt", "attempt_count >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_duplicate_matches_analysis_run_id_historical_ticket_id",
                table: "duplicate_matches",
                columns: new[] { "analysis_run_id", "historical_ticket_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_duplicate_matches_analysis_run_id_rank",
                table: "duplicate_matches",
                columns: new[] { "analysis_run_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_embedding_cache_content_hash_provider_model_embedding_versi~",
                table: "embedding_cache",
                columns: new[] { "content_hash", "provider", "model", "embedding_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historical_tickets_embedding_version_indexed_at",
                table: "historical_tickets",
                columns: new[] { "embedding_version", "indexed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_historical_tickets_project_id_source_external_id",
                table: "historical_tickets",
                columns: new[] { "project_id", "source", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_historical_tickets_project_id_status",
                table: "historical_tickets",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_historical_tickets_stack_signature",
                table: "historical_tickets",
                column: "stack_signature");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_historical_tickets_search_vector ON historical_tickets USING GIN (to_tsvector('simple', search_text));");

            migrationBuilder.CreateIndex(
                name: "IX_historical_ticket_index_jobs_status_available_at",
                table: "historical_ticket_index_jobs",
                columns: new[] { "status", "available_at" });

            migrationBuilder.CreateIndex(
                name: "IX_historical_ticket_index_jobs_ticket_id",
                table: "historical_ticket_index_jobs",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_import_batches_project_id_source_file_hash_import_ve~",
                table: "ticket_import_batches",
                columns: new[] { "project_id", "source", "file_hash", "import_version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "duplicate_matches");

            migrationBuilder.DropTable(
                name: "embedding_cache");

            migrationBuilder.DropTable(
                name: "historical_tickets");

            migrationBuilder.DropTable(
                name: "historical_ticket_index_jobs");

            migrationBuilder.DropTable(
                name: "ticket_import_batches");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
