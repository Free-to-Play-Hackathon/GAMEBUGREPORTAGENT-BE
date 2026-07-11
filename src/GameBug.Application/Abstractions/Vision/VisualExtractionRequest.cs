using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;

namespace GameBug.Application.Abstractions.Vision;

public sealed record VisualExtractionRequest(
    AnalysisRunId AnalysisRunId,
    IReadOnlyList<VisualAttachmentDescriptor> Attachments,
    string Provider,
    string StageVersion);

public sealed record VisualAttachmentDescriptor(
    AttachmentId AttachmentId,
    string ContentType,
    long SizeBytes,
    string Checksum,
    Func<CancellationToken, Task<Stream>> OpenReadAsync);
