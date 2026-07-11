using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Duplicates;
using GameBug.Domain.Duplicates;

namespace GameBug.Infrastructure.Seeding;

public sealed class DemoDataSeeder
{
    private const string Source = "demo";

    private readonly IHistoricalTicketRepository _tickets;
    private readonly IUnitOfWork _unitOfWork;

    public DemoDataSeeder(
        IHistoricalTicketRepository tickets,
        IUnitOfWork unitOfWork)
    {
        _tickets = tickets;
        _unitOfWork = unitOfWork;
    }

    public async Task SeedAsync(string datasetVersion, CancellationToken cancellationToken)
    {
        if (!datasetVersion.Equals("demo-v1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only demo-v1 can be seeded by the MVP seeder.");
        }

        foreach (var ticket in BuildTickets(datasetVersion))
        {
            var existing = await _tickets.GetByExternalIdAsync(
                ticket.ProjectId,
                ticket.Source,
                ticket.ExternalId,
                cancellationToken);
            if (existing is null)
            {
                await _tickets.SaveHistoricalTicketAsync(ticket, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyCollection<HistoricalTicket> BuildTickets(string datasetVersion)
    {
        return new[]
        {
            CreateTicket(
                "BUG-201",
                "Inventory crash",
                "Game crashes after opening inventory.",
                "open",
                "high",
                "InventoryPanel.Refresh",
                "open inventory",
                "inventory",
                "Application crashes"),
            CreateTicket(
                "BUG-202",
                "Reward missing after purchase",
                "Purchased reward is missing from equipment list.",
                "open",
                "medium",
                null,
                "complete purchase",
                "store",
                "Reward does not appear")
        };

        HistoricalTicket CreateTicket(
            string externalId,
            string title,
            string summary,
            string status,
            string severity,
            string? stackSignature,
            string trigger,
            string scene,
            string actualResult)
        {
            string searchText = DuplicateTextNormalizer.BuildSearchText(
                DuplicateSearchDocumentBuilder.TemplateVersion,
                title,
                summary,
                trigger,
                scene,
                actualResult,
                stackSignature,
                "Windows",
                "1.2.3");

            return HistoricalTicket.Create(
                Guid.NewGuid(),
                DuplicateSearchDocumentBuilder.DefaultProjectId,
                Source,
                externalId,
                title,
                summary,
                status,
                severity,
                "1.0.0",
                "2.0.0",
                new[] { "Windows" },
                stackSignature,
                stackSignature,
                new[] { scene },
                summary,
                trigger,
                scene,
                actualResult,
                searchText,
                DuplicateTextNormalizer.Hash(searchText),
                datasetVersion,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow).Value;
        }
    }
}
