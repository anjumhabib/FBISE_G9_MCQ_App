using System.IdentityModel.Tokens.Jwt;
using McqExam.Application;
using McqExam.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace McqExam.WebAPI;

[ApiController, Route("api/auth"), EnableRateLimiting("authentication")]
public sealed class AuthController(AuthService service, RefreshSessions sessions) : ControllerBase
{
    private const string RefreshCookie = "mcq-refresh";
    private static readonly CookieBuilder Cookie = new()
    {
        Name = RefreshCookie, HttpOnly = true, SameSite = SameSiteMode.Lax,
        Path = "/api/auth", SecurePolicy = CookieSecurePolicy.SameAsRequest
    };

    private AuthSession SetRefreshCookie(RenewedSession session)
    {
        var options = Cookie.Build(HttpContext);
        options.Expires = new DateTimeOffset(session.ExpiresAt, TimeSpan.Zero);
        Response.Cookies.Append(RefreshCookie, session.Token, options);
        return session.Access;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthSession>> Register(RegisterRequest request, CancellationToken ct) =>
        StatusCode(201, SetRefreshCookie(await sessions.Start(await service.Register(request, ct), Request.Cookies[RefreshCookie], ct)));

    [HttpPost("login")]
    [TrackVisit(UserVisitEventType.Login)]
    public async Task<AuthSession> Login(LoginRequest request, CancellationToken ct) =>
        SetRefreshCookie(await sessions.Start(await service.Login(request, ct), Request.Cookies[RefreshCookie], ct));

    [HttpPost("refresh")]
    [DisableRateLimiting]
    [Consumes("application/json")]
    public async Task<ActionResult<AuthSession>> Refresh(CancellationToken ct)
    {
        if (!Request.HasJsonContentType()) return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        var session = await sessions.Renew(Request.Cookies[RefreshCookie], ct);
        return session is null ? Unauthorized() : SetRefreshCookie(session);
    }

    [HttpPost("logout")]
    [DisableRateLimiting]
    [Consumes("application/json")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (!Request.HasJsonContentType()) return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        await sessions.Logout(Request.Cookies[RefreshCookie], ct);
        Response.Cookies.Delete(RefreshCookie, Cookie.Build(HttpContext));
        return NoContent();
    }
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
