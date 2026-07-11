using GameBug.Application.Abstractions.Filing;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Abstractions.Observability;
using GameBug.Application.Abstractions.Time;
using GameBug.Application.QaWorkflow;
using GameBug.Domain.QaWorkflow;
using GameBug.Domain.SharedKernel;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace GameBug.Application.QaWorkflow.CreateTicket;

internal sealed class CreateTicketCommandHandler : IRequestHandler<CreateTicketCommand, Result>
{
    private readonly IQaReviewRepository _reviewRepository;
    private readonly IAnalysisRunRepository _analysisRunRepository;
    private readonly IBugReportRepository _bugReportRepository;
    private readonly ITicketFilingGateway _ticketFilingGateway;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditWriter _auditWriter;

    public CreateTicketCommandHandler(
        IQaReviewRepository reviewRepository,
        IAnalysisRunRepository analysisRunRepository,
        IBugReportRepository bugReportRepository,
        ITicketFilingGateway ticketFilingGateway,
        IIdempotencyStore idempotencyStore,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditWriter auditWriter)
    {
        _reviewRepository = reviewRepository;
        _analysisRunRepository = analysisRunRepository;
        _bugReportRepository = bugReportRepository;
        _ticketFilingGateway = ticketFilingGateway;
        _idempotencyStore = idempotencyStore;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _auditWriter = auditWriter;
    }

    public async Task<Result> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result.Failure(new DomainError("Auth.Unauthorized", "User is not authenticated."));
        }

        string requestHash = QaWorkflowIdempotency.Hash(
            request.AnalysisRunId, request.FinalRevisionId, request.CandidateSnapshotHash, request.ExpectedVersion, request.Notes);
        string scopedKey = QaWorkflowIdempotency.Scope(
            _currentUser.UserId,
            "POST",
            $"/api/v1/analyses/{request.AnalysisRunId}/decisions/new-ticket",
            request.IdempotencyKey);
        var idempotency = await QaWorkflowIdempotency.ReserveAsync(
            _idempotencyStore, _clock, scopedKey, requestHash, cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result.Failure(idempotency.Error);
        }

        if (idempotency.Value.IsReplay)
        {
            return Result.Success();
        }

        var review = await _reviewRepository.GetByAnalysisRunIdAsync(new Domain.Analysis.AnalysisRunId(request.AnalysisRunId), cancellationToken);

        if (review == null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("DUPLICATE_GATE_REQUIRED", "QA Review not found."));
        }

        var matches = await _analysisRunRepository.GetDuplicateMatchesAsync(review.AnalysisRunId, cancellationToken);
        if (matches.Count == 0 || matches.Any(match => match.CandidateSnapshotHash != review.CandidateSnapshotHash))
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("DUPLICATE_GATE_REQUIRED", "A persisted duplicate candidate snapshot is required before creating a ticket."));
        }

        var finalRevision = review.Revisions.FirstOrDefault(revision => revision.Id.Value == request.FinalRevisionId);
        if (finalRevision is null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("CreateTicket.FinalRevisionRequired", "Final revision must belong to the QA review."));
        }

        var createResult = review.CreateNewTicket(
            request.CandidateSnapshotHash,
            request.ExpectedVersion,
            _currentUser.UserId!,
            _clock.UtcNow,
            request.Notes);

        if (createResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return createResult;
        }

        // Generate filing payload
        var analysisRun = await _analysisRunRepository.GetAsync(review.AnalysisRunId, cancellationToken);
        if (analysisRun is null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("CreateTicket.AnalysisNotFound", "Analysis run not found."));
        }

        var report = await _bugReportRepository.GetAsync(new GameBug.Domain.BugReports.BugReportId(analysisRun.ReportId.Value), cancellationToken);
        if (report is null)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(new DomainError("CreateTicket.ReportNotFound", "Bug report not found."));
        }

        // A canonical payload representation for hashing
        string payloadData = $"{review.Id.Value}|{finalRevision.Id.Value}|{finalRevision.SerializedRepro}";
        string payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadData)));
        
        string filingIdempotencyKey = $"filing:{review.Id.Value}";
        
        // File the ticket via gateway
        var filingResult = await _ticketFilingGateway.FileTicketAsync(
            filingIdempotencyKey,
            payloadHash,
            $"Bug Report {report.Id.Value}",
            finalRevision.SerializedRepro,
            _currentUser.UserId!,
            cancellationToken);

        if (filingResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return Result.Failure(filingResult.Error); // Rollback will happen
        }

        var ticketData = filingResult.Value;

        var attachResult = review.AttachInternalTicket(
            ticketData.ExternalTicketId,
            ticketData.SystemName,
            ticketData.Url,
            ticketData.FiledAt);

        if (attachResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return attachResult;
        }

        var reportUpdateResult = report.CloseWithNewTicket(_clock.UtcNow);
        if (reportUpdateResult.IsFailure)
        {
            await ReleaseReservationAsync(idempotency.Value, cancellationToken);
            return reportUpdateResult;
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await QaWorkflowIdempotency.CompleteAsync(
            _idempotencyStore, idempotency.Value, review.Id.Value, _clock, cancellationToken);

        await _auditWriter.WriteAsync(
            "QaReview",
            review.Id.Value,
            "TicketCreated",
            _currentUser.UserId!,
            new { ExpectedVersion = request.ExpectedVersion, ExternalTicketId = ticketData.ExternalTicketId },
            cancellationToken);

        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        return Result.Success();
    }

    private async Task ReleaseReservationAsync(QaIdempotencyReservation reservation, CancellationToken cancellationToken)
    {
        _unitOfWork.ClearChanges();
        await QaWorkflowIdempotency.ReleaseAsync(_idempotencyStore, reservation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
