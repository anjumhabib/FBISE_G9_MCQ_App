using McqExam.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace McqExam.WebAPI;

[ApiController, Authorize(Roles = "Admin"), Route("api/admin")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class AdminController(AdminReportService reports, AdminAuditService audit, AdminTrafficService traffic,
    CsvDownload csv) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboard>> Dashboard([FromQuery] AdminReportFilter filter, CancellationToken ct)
    {
        var dashboard = await reports.Dashboard(filter, ct);
        return filter.ExportCsv ? csv.File("dashboard", new[] { dashboard }) : dashboard;
    }

    [HttpGet("reports/students")]
    public async Task<ActionResult<AdminPage<AdminStudentRow>>> Students([FromQuery] AdminStudentFilter filter,
        CancellationToken ct) => Download("students", filter, await reports.Students(filter, ct));

    [HttpGet("reports/student-subjects")]
    public async Task<ActionResult<AdminPage<AdminStudentSubjectRow>>> StudentSubjects([FromQuery] AdminReportFilter filter,
        CancellationToken ct) => Download("student-subjects", filter, await reports.StudentSubjects(filter, ct));

    [HttpGet("reports/subjects")]
    public async Task<ActionResult<AdminPage<AdminSubjectRow>>> Subjects([FromQuery] AdminReportFilter filter,
        CancellationToken ct) => Download("subjects", filter, await reports.Subjects(filter, ct));

    [HttpGet("attempts/{attemptId:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminAttemptDetail>> Attempt(Guid attemptId, [FromQuery] AdminExportFilter filter,
        CancellationToken ct)
    {
        var detail = await reports.Attempt(attemptId, ct);
        if (!filter.ExportCsv)
            return detail;

        var rows = detail.Questions.Select(question => new { detail.Student, detail.Attempt, detail.Chapters, detail.Bloom, Question = question });
        return csv.File($"attempt-{attemptId}", rows);
    }

    [HttpGet("reports/questions")]
    public async Task<ActionResult<AdminPage<AdminQuestionRow>>> Questions([FromQuery] AdminQuestionFilter filter,
        CancellationToken ct) => Download("questions", filter, await audit.Questions(filter, ct));

    [HttpGet("reports/duplicates")]
    public async Task<ActionResult<AdminPage<AdminDuplicateGroup>>> Duplicates([FromQuery] AdminReportFilter filter,
        CancellationToken ct)
    {
        var page = await audit.Duplicates(filter, ct);
        if (!filter.ExportCsv)
            return page;

        var rows = page.Items.SelectMany(group => group.Questions.Select(item =>
            new { group.NormalizedText, group.HasExactDuplicates, item.Question, item.AttemptCount, item.PresentationDevices }));
        return csv.File("duplicates", rows);
    }

    [HttpGet("reports/media-audit")]
    public async Task<ActionResult<AdminPage<AdminMediaIssue>>> MediaAudit([FromQuery] AdminReportFilter filter,
        CancellationToken ct) => Download("media-audit", filter, await audit.MediaAudit(filter, ct));

    [HttpGet("reports/visits")]
    public async Task<ActionResult<AdminVisitsReport>> Visits([FromQuery] AdminVisitFilter filter, CancellationToken ct)
    {
        var report = await traffic.Visits(filter, ct);
        return filter.ExportCsv ? csv.File("visits", report.Visits.Items) : report;
    }

    [HttpGet("reports/anomalies")]
    public async Task<ActionResult<AdminPage<AdminAnomaly>>> Anomalies([FromQuery] AdminReportFilter filter,
        CancellationToken ct) => Download("anomalies", filter, await reports.Anomalies(filter, ct));

    private ActionResult<AdminPage<T>> Download<T>(string name, AdminReportFilter filter, AdminPage<T> page) =>
        filter.ExportCsv ? csv.File(name, page.Items) : page;
}
