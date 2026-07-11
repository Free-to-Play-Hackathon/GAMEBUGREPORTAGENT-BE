using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameBug.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase34ReliabilityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_historical_tickets_embedding_version_indexed_at",
                table: "historical_tickets");

            migrationBuilder.DropIndex(
                name: "IX_embedding_cache_content_hash_provider_model_embedding_versi~",
                table: "embedding_cache");

            migrationBuilder.DropIndex(
                name: "IX_analysis_jobs_analysis_run_id",
                table: "analysis_jobs");

            migrationBuilder.Sql("""
                DELETE FROM analysis_jobs
                WHERE id IN (
                    SELECT id
                    FROM (
                        SELECT id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY queue_name, analysis_run_id, expected_version
                                   ORDER BY CASE status
                                       WHEN 'Completed' THEN 0
                                       WHEN 'Processing' THEN 1
                                       WHEN 'Queued' THEN 2
                                       ELSE 3
                                   END,
                                   created_at,
                                   id
                               ) AS duplicate_number
                        FROM analysis_jobs
                    ) ranked_jobs
                    WHERE duplicate_number > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_historical_tickets_embedding_version_embedding_dimension_in~",
                table: "historical_tickets",
                columns: new[] { "embedding_version", "embedding_dimension", "indexed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_embedding_cache_content_hash_provider_model_embedding_versi~",
                table: "embedding_cache",
                columns: new[] { "content_hash", "provider", "model", "embedding_version", "dimension" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_analysis_jobs_queue_name_analysis_run_id_expected_version",
                table: "analysis_jobs",
                columns: new[] { "queue_name", "analysis_run_id", "expected_version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_historical_tickets_embedding_version_embedding_dimension_in~",
                table: "historical_tickets");

            migrationBuilder.DropIndex(
                name: "IX_embedding_cache_content_hash_provider_model_embedding_versi~",
                table: "embedding_cache");

            migrationBuilder.DropIndex(
                name: "IX_analysis_jobs_queue_name_analysis_run_id_expected_version",
                table: "analysis_jobs");

            migrationBuilder.CreateIndex(
                name: "IX_historical_tickets_embedding_version_indexed_at",
                table: "historical_tickets",
                columns: new[] { "embedding_version", "indexed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_embedding_cache_content_hash_provider_model_embedding_versi~",
                table: "embedding_cache",
                columns: new[] { "content_hash", "provider", "model", "embedding_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_analysis_jobs_analysis_run_id",
                table: "analysis_jobs",
                column: "analysis_run_id");
        }
    }
}
