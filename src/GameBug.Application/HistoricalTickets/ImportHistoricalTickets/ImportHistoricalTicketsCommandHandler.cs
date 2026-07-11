using System.Text.Json;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Duplicates;
using GameBug.Domain.Duplicates;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.HistoricalTickets.ImportHistoricalTickets;

public sealed class ImportHistoricalTicketsCommandHandler : IRequestHandler<ImportHistoricalTicketsCommand, Result<ImportHistoricalTicketsResult>>
{
    private readonly IHistoricalTicketRepository _tickets;
    private readonly IContentSanitizer _sanitizer;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    private readonly IHistoricalTicketIndexQueue _indexQueue;

    public ImportHistoricalTicketsCommandHandler(
        IHistoricalTicketRepository tickets,
        IContentSanitizer sanitizer,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IHistoricalTicketIndexQueue indexQueue)
    {
        _tickets = tickets;
        _sanitizer = sanitizer;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _indexQueue = indexQueue;
    }

    public async Task<Result<ImportHistoricalTicketsResult>> Handle(ImportHistoricalTicketsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure<ImportHistoricalTicketsResult>(new DomainError("Auth.Unauthorized", "Authentication is required."));
        }

        if (request.Items.Count is <= 0 or > 500)
        {
            return Result.Failure<ImportHistoricalTicketsResult>(new DomainError("HistoricalTicketImport.InvalidItemCount", "Import must contain between 1 and 500 tickets."));
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Result.Failure<ImportHistoricalTicketsResult>(new DomainError("Validation.IdempotencyKeyRequired", "Idempotency-Key header is required."));
        }

        Guid projectId = request.ProjectId == Guid.Empty ? DuplicateSearchDocumentBuilder.DefaultProjectId : request.ProjectId;
        string fileHash = DuplicateTextNormalizer.Hash(JsonSerializer.Serialize(request.Items));
        var replay = await _tickets.GetImportBatchByHashAsync(projectId, request.Source, fileHash, request.ImportVersion, cancellationToken);
        if (replay is not null)
        {
            IReadOnlyList<HistoricalTicketImportError> replayErrors = string.IsNullOrWhiteSpace(replay.ErrorsJson)
                ? Array.Empty<HistoricalTicketImportError>()
                : JsonSerializer.Deserialize<List<HistoricalTicketImportError>>(replay.ErrorsJson) ?? new List<HistoricalTicketImportError>();
            return new ImportHistoricalTicketsResult(replay.Id, replay.Status, replay.AcceptedCount, replay.RejectedCount, replayErrors);
        }

        var batch = new TicketImportBatch(
            Guid.NewGuid(),
            projectId,
            request.Source.Trim(),
            fileHash,
            request.ImportVersion.Trim(),
            _currentUser.UserId!,
            DateTimeOffset.UtcNow);

        await _tickets.SaveImportBatchAsync(batch, cancellationToken);

        var duplicateExternalIds = request.Items
            .GroupBy(i => i.ExternalId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var errors = new List<HistoricalTicketImportError>();
        int accepted = 0;
        foreach (var item in request.Items)
        {
            if (duplicateExternalIds.Contains(item.ExternalId))
            {
                errors.Add(new HistoricalTicketImportError(item.ExternalId, "DUPLICATE_EXTERNAL_ID", "External id appears more than once in this import."));
                continue;
            }

            var ticketResult = BuildTicket(projectId, request.Source, request.ImportVersion, item);
            if (ticketResult.IsFailure)
            {
                errors.Add(new HistoricalTicketImportError(item.ExternalId, ticketResult.Error.Code, ticketResult.Error.Description));
                continue;
            }

            var imported = ticketResult.Value;
            var existing = await _tickets.GetByExternalIdAsync(projectId, request.Source, imported.ExternalId, cancellationToken);
            Guid indexTicketId;
            if (existing is null)
            {
                await _tickets.SaveHistoricalTicketAsync(imported, cancellationToken);
                indexTicketId = imported.Id;
            }
            else
            {
                existing.UpdateFromImport(imported, DateTimeOffset.UtcNow);
                indexTicketId = existing.Id;
            }

            await _indexQueue.EnqueueAsync(indexTicketId, cancellationToken);
            accepted++;
        }

        batch.Complete(accepted, errors.Count, JsonSerializer.Serialize(errors), DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ImportHistoricalTicketsResult(batch.Id, batch.Status, accepted, errors.Count, errors);
    }

    private Result<HistoricalTicket> BuildTicket(
        Guid projectId,
        string source,
        string importVersion,
        HistoricalTicketImportItem item)
    {
        string sanitizedTitle = _sanitizer.Sanitize(item.Title);
        string sanitizedSummary = _sanitizer.Sanitize(item.Summary);
        string sanitizedStackSummary = item.StackSummary is null ? string.Empty : _sanitizer.Sanitize(item.StackSummary);
        string searchText = DuplicateTextNormalizer.BuildSearchText(
            DuplicateSearchDocumentBuilder.TemplateVersion,
            sanitizedTitle,
            sanitizedSummary,
            item.Symptom,
            item.TriggerAction,
            item.SceneOrFeature,
            item.ActualResult,
            item.StackSignature,
            sanitizedStackSummary,
            string.Join(' ', item.GameEntities),
            string.Join(' ', item.Platforms),
            item.BuildMin,
            item.BuildMax);

        var ticketResult = HistoricalTicket.Create(
            Guid.NewGuid(),
            projectId,
            source,
            item.ExternalId,
            sanitizedTitle,
            sanitizedSummary,
            item.Status,
            item.Severity,
            item.BuildMin,
            item.BuildMax,
            item.Platforms,
            item.StackSignature,
            sanitizedStackSummary,
            item.GameEntities,
            item.Symptom,
            item.TriggerAction,
            item.SceneOrFeature,
            item.ActualResult,
            searchText,
            DuplicateTextNormalizer.Hash(searchText),
            importVersion,
            item.SourceUpdatedAt ?? DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        if (ticketResult.IsFailure)
        {
            return ticketResult;
        }

        var ticket = ticketResult.Value;
        return ticket;
    }
}
