using System.Text.Json;
using System.Text.Json.Serialization;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Duplicates;
using GameBug.Application.HistoricalTickets.ImportHistoricalTickets;
using GameBug.Domain.Duplicates;
using GameBug.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Infrastructure.Seeding;

public sealed class DemoDataSeeder
{
    private const string Source = "screenshots";
    private readonly IHistoricalTicketRepository _tickets;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GameBugDbContext _dbContext;
    private readonly IHistoricalTicketIndexQueue _indexQueue;

    public DemoDataSeeder(
        IHistoricalTicketRepository tickets,
        IUnitOfWork unitOfWork,
        GameBugDbContext dbContext,
        IHistoricalTicketIndexQueue indexQueue)
    {
        _tickets = tickets;
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
        _indexQueue = indexQueue;
    }

    public async Task SeedAsync(string datasetVersion, CancellationToken cancellationToken)
    {
        if (!datasetVersion.Equals("screenshots-v1", StringComparison.OrdinalIgnoreCase) &&
            !datasetVersion.Equals("demo-v1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only screenshots-v1 (or the demo-v1 compatibility alias) can be seeded.");
        }

        string datasetDirectory = Path.Combine(AppContext.BaseDirectory, "screenshots");
        string ticketsPath = Path.Combine(datasetDirectory, "tickets.json");
        string labelsPath = Path.Combine(datasetDirectory, "labels.json");
        if (!File.Exists(ticketsPath) || !File.Exists(labelsPath))
        {
            throw new FileNotFoundException(
                $"Screenshot dataset is incomplete. Expected tickets.json and labels.json under '{datasetDirectory}'.");
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
        };
        await using FileStream ticketStream = File.OpenRead(ticketsPath);
        var ticketRows = await JsonSerializer.DeserializeAsync<List<ScreenshotTicket>>(ticketStream, jsonOptions, cancellationToken)
            ?? throw new InvalidDataException("screenshots/tickets.json must contain a JSON array.");
        await using FileStream labelStream = File.OpenRead(labelsPath);
        var labelRows = await JsonSerializer.DeserializeAsync<List<ScreenshotLabel>>(labelStream, jsonOptions, cancellationToken)
            ?? throw new InvalidDataException("screenshots/labels.json must contain a JSON array.");

        var labelsByTicket = labelRows
            .SelectMany(label => label.LinkedTicketIds.Select(ticketId => (ticketId, label)))
            .GroupBy(item => item.ticketId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.label).ToArray(), StringComparer.OrdinalIgnoreCase);

        // Remove only the two records produced by the former hard-coded seeder.
        // User-imported and other demo-source records remain untouched.
        await _dbContext.HistoricalTickets
            .Where(ticket => ticket.Source == "demo" &&
                (ticket.ExternalId == "BUG-201" || ticket.ExternalId == "BUG-202"))
            .ExecuteDeleteAsync(cancellationToken);

        DateTimeOffset importedAt = DateTimeOffset.UtcNow;
        foreach (ScreenshotTicket row in ticketRows)
        {
            HistoricalTicket imported = BuildTicket(row, labelsByTicket.GetValueOrDefault(row.TicketId) ?? [], importedAt);
            HistoricalTicket? existing = await _tickets.GetByExternalIdAsync(
                imported.ProjectId, imported.Source, imported.ExternalId, cancellationToken);
            if (existing is null)
            {
                await _tickets.SaveHistoricalTicketAsync(imported, cancellationToken);
                await _indexQueue.EnqueueAsync(imported.Id, cancellationToken);
            }
            else
            {
                existing.UpdateFromImport(imported, importedAt);
                await _indexQueue.EnqueueAsync(existing.Id, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static HistoricalTicket BuildTicket(
        ScreenshotTicket row,
        IReadOnlyCollection<ScreenshotLabel> labels,
        DateTimeOffset importedAt)
    {
        if (string.IsNullOrWhiteSpace(row.TicketId) || string.IsNullOrWhiteSpace(row.Title))
        {
            throw new InvalidDataException("Every screenshot ticket requires ticket_id and title.");
        }

        string trigger = FirstNotEmpty(row.Action, labels.Select(label => label.Action).FirstOrDefault());
        string scene = FirstNotEmpty(row.Screen, row.Feature, labels.Select(label => label.Screen).FirstOrDefault());
        string actualResult = FirstNotEmpty(row.ActualResult, labels.Select(label => label.ActualBehavior).FirstOrDefault());
        string summary = $"{actualResult} Expected: {row.ExpectedResult}".Trim();
        string[] evidence = labels.Select(label => label.FileName).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        string symptom = evidence.Length == 0 ? summary : $"{summary} Visual evidence: {string.Join(", ", evidence)}";
        string searchText = DuplicateTextNormalizer.BuildSearchText(
            DuplicateSearchDocumentBuilder.TemplateVersion,
            row.Title,
            summary,
            trigger,
            scene,
            actualResult,
            null,
            "game-client",
            null);

        return HistoricalTicket.Create(
            Guid.NewGuid(), DuplicateSearchDocumentBuilder.DefaultProjectId, Source, row.TicketId,
            row.Title, summary, row.Status, row.Severity, null, null, ["game-client"],
            null, null, [row.Feature, scene], symptom, trigger, scene, actualResult,
            searchText, DuplicateTextNormalizer.Hash(searchText), "screenshots-v1", importedAt, importedAt).Value;
    }

    private static string FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record ScreenshotTicket(
        [property: JsonPropertyName("ticket_id")] string TicketId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("feature")] string Feature,
        [property: JsonPropertyName("screen")] string Screen,
        [property: JsonPropertyName("action")] string? Action,
        [property: JsonPropertyName("actual_result")] string ActualResult,
        [property: JsonPropertyName("expected_result")] string ExpectedResult,
        [property: JsonPropertyName("severity")] string Severity,
        [property: JsonPropertyName("status")] string Status);

    private sealed record ScreenshotLabel(
        [property: JsonPropertyName("file_name")] string FileName,
        [property: JsonPropertyName("screen")] string Screen,
        [property: JsonPropertyName("action")] string Action,
        [property: JsonPropertyName("actual_behavior")] string ActualBehavior,
        [property: JsonPropertyName("linked_ticket_ids")] string[] LinkedTicketIds);
}
