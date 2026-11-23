using System.IdentityModel.Tokens.Jwt;
using McqExam.Application;
using McqExam.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace McqExam.WebAPI;

[ApiController, Route("api/auth"), EnableRateLimiting("authentication")]
public sealed class AuthController(AuthService service) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthSession>> Register(RegisterRequest request, CancellationToken ct) =>
        StatusCode(201, await service.Register(request, ct));

    [HttpPost("login")]
    [TrackVisit(UserVisitEventType.Login)]
    public Task<AuthSession> Login(LoginRequest request, CancellationToken ct) => service.Login(request, ct);
}

[ApiController, Route("api/subjects")]
public sealed class SubjectsController(ExamService service) : ControllerBase
{
    [HttpGet]
    [TrackVisit(UserVisitEventType.PageView)]
    public Task<IReadOnlyList<SubjectInfo>> Get(CancellationToken ct) => service.Subjects(ct);
}

[ApiController, Authorize, Route("api/exams")]
public sealed class ExamsController(ExamService service) : ControllerBase
{
    [HttpPost("start")]
    [TrackVisit(UserVisitEventType.ExamStart)]
    public async Task<ActionResult<ActiveExam>> Start(StartRequest request, CancellationToken ct)
    {
        var exam = await service.Start(Student.Id(User), request, ct, ClientDevice.Classify(Request.Headers.UserAgent));
        return CreatedAtAction(nameof(Resume), new { attemptId = exam.AttemptId }, exam);
    }

    [HttpGet("{attemptId:guid}")]
    [TrackVisit(UserVisitEventType.PageView)]
    public Task<ActiveExam> Resume(Guid attemptId, CancellationToken ct) => service.Resume(attemptId, Student.Id(User), ct);

    [HttpPut("{attemptId:guid}/answers")]
    public async Task<IActionResult> SaveDraft(Guid attemptId, AnswerInput answer, CancellationToken ct)
    {
        await service.SaveDraft(attemptId, Student.Id(User), answer, ct);
        return NoContent();
    }

    [HttpPost("{attemptId:guid}/submit")]
    [TrackVisit(UserVisitEventType.ExamSubmit)]
    public Task<Result> Submit(Guid attemptId, SubmitRequest request, CancellationToken ct) =>
        service.Submit(attemptId, Student.Id(User), request, ct);

    [HttpGet("{attemptId:guid}/review")]
    [TrackVisit(UserVisitEventType.PageView)]
    public Task<Review> Review(Guid attemptId, CancellationToken ct) => service.Review(attemptId, Student.Id(User), ct);
}

[ApiController, Authorize, Route("api/reports")]
public sealed class ReportsController(ExamService service) : ControllerBase
{
    [HttpGet("history")]
    [TrackVisit(UserVisitEventType.PageView)]
    public Task<History> History(CancellationToken ct) => service.History(Student.Id(User), ct);
}

internal static class Student
{
    internal static int Id(System.Security.Claims.ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value, System.Globalization.CultureInfo.InvariantCulture);
}
