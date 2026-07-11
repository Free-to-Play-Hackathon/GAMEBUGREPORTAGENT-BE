$files = Get-ChildItem -Path "src\GameBug.Application\QaWorkflow" -Recurse -Filter "*.cs"
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $content = $content -replace "new Error\(", "new DomainError("
    $content = $content -replace "_currentUser.Id", "_currentUser.UserId!"
    $content = $content -replace "_bugReportRepository.GetByIdAsync\((.*?)analysisRun!.ReportId,(.*?)\)", "_bugReportRepository.GetAsync(new GameBug.Domain.BugReports.BugReportId(`$1analysisRun!.ReportId.Value),`$2)"
    $content = $content -replace "_bugReportRepository.GetByIdAsync\((.*?)analysisRun.ReportId,(.*?)\)", "_bugReportRepository.GetAsync(new GameBug.Domain.BugReports.BugReportId(`$1analysisRun.ReportId.Value),`$2)"
    $content = $content -replace "_bugReportRepository.GetByIdAsync\(oldAnalysisRun!.ReportId,", "_bugReportRepository.GetAsync(new GameBug.Domain.BugReports.BugReportId(oldAnalysisRun!.ReportId.Value),"
    Set-Content -Path $file.FullName -Value $content -NoNewline
}
