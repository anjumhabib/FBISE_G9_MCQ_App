using McqExam.Domain;
using Microsoft.AspNetCore.Mvc;

namespace McqExam.WebAPI;

[ApiExplorerSettings(IgnoreApi = true)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[TrackVisit(UserVisitEventType.PageView)]
public sealed class PagesController : Controller
{
    [HttpGet("/login")]
    public IActionResult Login() => Screen("Auth", "Sign in", "login");

    [HttpGet("/register")]
    public IActionResult Register() => Screen("Auth", "Create account", "register");

    [HttpGet("/setup")]
    public IActionResult Setup() => Screen("Setup", "New test", "setup");

    [HttpGet("/exam/{attemptId}")]
    public IActionResult Exam(string attemptId) => Screen("Exam", "Test", "exam", attemptId);

    [HttpGet("/review/{attemptId}")]
    public IActionResult Review(string attemptId) => Screen("Review", "Review", "review", attemptId);

    [HttpGet("/history")]
    public IActionResult History() => Screen("History", "History", "history");

    private IActionResult Screen(string view, string title, string page, string? attemptId = null)
    {
        ViewData["Title"] = title;
        ViewData["Page"] = page;
        ViewData["AttemptId"] = attemptId;
        return View(view);
    }
}
