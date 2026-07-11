namespace GameBug.Application.QaWorkflow.ReviseRepro;

public record ReviseReproCommand(
    Guid AnalysisRunId,
    Guid? BaseReproId,
    string SerializedRepro,
    int ExpectedVersion,
    string IdempotencyKey) : MediatR.IRequest<Domain.SharedKernel.Result>;
