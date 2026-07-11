using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameBug.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixPostgresConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE analysis_runs
                ALTER COLUMN version_token TYPE bigint
                USING version_token::text::bigint;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE analysis_runs
                ALTER COLUMN version_token TYPE xid
                USING version_token::text::xid;
                """);
        }
    }
}
