namespace GameBug.Contracts.BugReports;

public sealed record StartAnalysisRequest(
    string RequestedSchemaVersion,
    string ConfigurationProfile);

public sealed record StartAnalysisResponse(
    Guid AnalysisId,
    Guid ReportId,
    int Version,
    string Status,
    string StatusUrl,
    string ResultUrl);
