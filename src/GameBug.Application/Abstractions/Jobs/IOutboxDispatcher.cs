namespace GameBug.Application.Abstractions.Jobs;

public interface IOutboxDispatcher
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken);
}
