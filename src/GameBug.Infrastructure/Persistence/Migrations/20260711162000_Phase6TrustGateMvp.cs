using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameBug.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6TrustGateMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trust_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    policy_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    input_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    violations_json = table.Column<string>(type: "jsonb", nullable: false),
                    allowed_actions_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trust_reports", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trust_reports_target_policy_hash",
                table: "trust_reports",
                columns: new[] { "target_type", "target_id", "policy_version", "input_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trust_reports");
        }
    }
}
