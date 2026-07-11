namespace GameBug.Application.QaWorkflow.RequestInformation;

public record RequestInformationCommand(
    Guid AnalysisRunId,
    List<string> Questions,
    int ExpectedVersion,
    string IdempotencyKey) : MediatR.IRequest<Domain.SharedKernel.Result<Guid>>;
