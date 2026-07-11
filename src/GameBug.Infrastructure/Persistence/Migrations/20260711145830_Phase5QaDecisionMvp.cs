using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameBug.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5QaDecisionMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "qa_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateSnapshotHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    OpenedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VersionToken = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qa_reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_filing_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_filing_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "clarification_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResultingAnalysisRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clarification_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clarification_requests_qa_reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "qa_reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "internal_tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalTicketId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SystemName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FiledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_internal_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_internal_tickets_qa_reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "qa_reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qa_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Actor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DuplicateOfTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qa_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qa_decisions_historical_tickets_DuplicateOfTicketId",
                        column: x => x.DuplicateOfTicketId,
                        principalTable: "historical_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_qa_decisions_qa_reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "qa_reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repro_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    BaseReproId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SerializedRepro = table.Column<string>(type: "text", nullable: false),
                    Editor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repro_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repro_revisions_qa_reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "qa_reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clarification_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clarification_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clarification_questions_clarification_requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "clarification_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clarification_answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswerText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AnsweredBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clarification_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clarification_answers_clarification_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "clarification_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clarification_answers_QuestionId",
                table: "clarification_answers",
                column: "QuestionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clarification_questions_RequestId",
                table: "clarification_questions",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_clarification_requests_ReviewId",
                table: "clarification_requests",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_internal_tickets_ReviewId",
                table: "internal_tickets",
                column: "ReviewId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qa_decisions_DuplicateOfTicketId",
                table: "qa_decisions",
                column: "DuplicateOfTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_qa_decisions_ReviewId",
                table: "qa_decisions",
                column: "ReviewId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qa_reviews_AnalysisRunId",
                table: "qa_reviews",
                column: "AnalysisRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repro_revisions_ReviewId_RevisionNumber",
                table: "repro_revisions",
                columns: new[] { "ReviewId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_filing_requests_IdempotencyKey",
                table: "ticket_filing_requests",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clarification_answers");

            migrationBuilder.DropTable(
                name: "internal_tickets");

            migrationBuilder.DropTable(
                name: "qa_decisions");

            migrationBuilder.DropTable(
                name: "repro_revisions");

            migrationBuilder.DropTable(
                name: "ticket_filing_requests");

            migrationBuilder.DropTable(
                name: "clarification_questions");

            migrationBuilder.DropTable(
                name: "clarification_requests");

            migrationBuilder.DropTable(
                name: "qa_reviews");
        }
    }
}
