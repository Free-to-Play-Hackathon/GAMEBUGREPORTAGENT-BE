using GameBug.Application.QaWorkflow.AnswerClarification;
using GameBug.Application.QaWorkflow.CreateTicket;
using GameBug.Application.QaWorkflow.GetReview;
using GameBug.Application.QaWorkflow.GetTriageEfficiency;
using GameBug.Application.QaWorkflow.MarkDuplicate;
using GameBug.Application.QaWorkflow.OpenReview;
using GameBug.Application.QaWorkflow.RejectAnalysis;
using GameBug.Application.QaWorkflow.RequestInformation;
using GameBug.Application.QaWorkflow.ReviseRepro;
using GameBug.Contracts.QaDecisions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GameBug.Api.Endpoints.QaDecisions;

public static class QaReviewEndpoints
{
    public static void MapQaReviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/qa/triage-efficiency", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetTriageEfficiencyQuery(), cancellationToken)))
            .WithTags("QA Workflow")
            .RequireAuthorization();

        var group = app.MapGroup("/api/v1/analyses/{analysisId:guid}")
            .WithTags("QA Workflow")
            .RequireAuthorization();

        group.MapPost("/review", async (Guid analysisId, [FromBody] OpenQaReviewRequest request, ISender sender, HttpRequest httpRequest, CancellationToken cancellationToken) =>
        {
            if (!TryGetIdempotencyKey(httpRequest, out string idempotencyKey, out IResult error))
            {
                return error;
            }

            var command = new OpenQaReviewCommand(analysisId, request.CandidateSnapshotHash, idempotencyKey);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Results.Conflict(result.Error);
            }

            var location = $"/api/v1/analyses/{analysisId}/review";
            return Results.Created(location, new { ReviewId = result.Value });
        });

        group.MapGet("/review", async (Guid analysisId, ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetQaReviewQuery(analysisId);
            var result = await sender.Send(query, cancellationToken);

            if (result.IsFailure)
            {
                return Results.NotFound(result.Error);
            }

            return Results.Ok(QaDecisionContractMapper.ToResponse(result.Value));
        });

        group.MapPut("/repro-case", async (Guid analysisId, [FromBody] ReviseReproRequest request, ISender sender, HttpRequest httpRequest, CancellationToken cancellationToken) =>
        {
            if (!TryGetIdempotencyKey(httpRequest, out string idempotencyKey, out IResult error))
            {
                return error;
            }

            var command = new ReviseReproCommand(analysisId, request.BaseReproId, request.SerializedRepro, request.ExpectedVersion, idempotencyKey);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Results.Conflict(result.Error);
            }

            return Results.NoContent();
        });

        group.MapPost("/decisions/duplicate", async (Guid analysisId, [FromBody] MarkDuplicateRequest request, ISender sender, HttpRequest httpRequest, CancellationToken cancellationToken) =>
        {
            if (!TryGetIdempotencyKey(httpRequest, out string idempotencyKey, out IResult error))
            {
                return error;
            }

            var command = new MarkDuplicateCommand(
                analysisId,
                request.DuplicateTicketId,
                request.CandidateSnapshotHash,
                request.ExpectedVersion,
                request.Notes,
                idempotencyKey);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Results.Conflict(result.Error);
            }

            return Results.NoContent();
        });

        group.MapPost("/decisions/new-ticket", async (Guid analysisId, [FromBody] CreateTicketRequest request, ISender sender, HttpRequest httpRequest, CancellationToken cancellationToken) =>
        {
            if (!TryGetIdempotencyKey(httpRequest, out string idempotencyKey, out IResult error))
            {
                return error;
            }

            var command = new CreateTicketCommand(
                analysisId,
                request.FinalRevisionId,
                request.CandidateSnapshotHash,
                request.ExpectedVersion,
                request.Notes,
                idempotencyKey);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Results.Conflict(result.Error);
            }

            return Results.NoContent();
        });

        group.MapPost("/decisions/reject", async (Guid analysisId, [FromBody] RejectAnalysisRequest request, ISender sender, HttpRequest httpRequest, CancellationToken cancellationToken) =>
        {
            if (!TryGetIdempotencyKey(httpRequest, out string idempotencyKey, out IResult error))
            {
                return error;
            }

            var command = new RejectAnalysisCommand(
                analysisId,
                request.ReasonCode,
                request.ExpectedVersion,
                request.Notes,
                idempotencyKey);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Results.Conflict(result.Error);
            }

            return Results.NoContent();
        });

        group.MapPost("/clarifications", async (Guid analysisId, [FromBody] RequestInformationRequest request, ISender sender, HttpRequest httpRequest, CancellationToken cancellationToken) =>
        {
            if (!TryGetIdempotencyKey(httpRequest, out string idempotencyKey, out IResult error))
            {
                return error;
            }

            var command = new RequestInformationCommand(
                analysisId,
                request.Questions,
                request.ExpectedVersion,
                idempotencyKey);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Results.Conflict(result.Error);
            }

            return Results.Ok(new { ClarificationRequestId = result.Value });
        });

        group.MapPost("/clarifications/{requestId:guid}/answers", async (Guid analysisId, Guid requestId, [FromBody] AnswerClarificationRequest request, ISender sender, HttpRequest httpRequest, CancellationToken cancellationToken) =>
        {
            if (!TryGetIdempotencyKey(httpRequest, out string idempotencyKey, out IResult error))
            {
                return error;
            }

            var answers = request.Answers.Select(a => new ClarificationAnswerInput
            {
                QuestionId = a.QuestionId,
                AnswerText = a.AnswerText
            }).ToList();

            var command = new AnswerClarificationCommand(analysisId, requestId, answers, idempotencyKey);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Results.Conflict(result.Error);
            }

            return Results.Ok(new { NewAnalysisRunId = result.Value });
        });
    }

    private static bool TryGetIdempotencyKey(HttpRequest request, out string idempotencyKey, out IResult error)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var values) || string.IsNullOrWhiteSpace(values))
        {
            idempotencyKey = string.Empty;
            error = Results.BadRequest(new
            {
                Code = "Validation.IdempotencyKeyRequired",
                Description = "Idempotency-Key header is required."
            });
            return false;
        }

        idempotencyKey = values.ToString();
        error = Results.Empty;
        return true;
    }
}
