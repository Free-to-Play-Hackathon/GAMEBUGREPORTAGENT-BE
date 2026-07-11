namespace GameBug.Domain.QaWorkflow;

public enum QaReviewStatus
{
    Open = 0,
    DuplicateMarked = 1,
    NewTicketCreated = 2,
    MoreInformationRequested = 3,
    Rejected = 4
}
